




// Pull kafka backend components here
// use them in boths scripts

// start pubishing results of jobs and see how it goes...
#I "../../packages/"
#r "Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "FSharp.Data/lib/net40/FSharp.Data.dll"

open FSharp.Data
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open System.IO
open System.Linq



(*


    Wrapper functionality


*)

[<AutoOpen>]
module Utility =

    type Result<'TSuccess,'TError> =
         | Success of 'TSuccess
         | Error of 'TError

    // Success value
    let inline sval (res:Result<'a,'b>) = match res with | Success a -> a | Error msg -> failwith (msg.ToString())


    let (|Exists|_|) (str:string) = if System.String.IsNullOrEmpty str then None else Some str

    let cleanJson (json:string) = json.Replace("\\\"", "\"")

    let splitJsonArray (input:Result<string,string>) =
        match input with
        | Success str ->
            match str with
            | Exists s ->
                str.Replace("[", "").Replace("]", "").Replace("\"", "").Split(',') |> Success
            | _ -> [||] |> Success
        | Error msg -> Error msg

    let splitIntArray (input:string) =
        input.Replace("[", "").Replace("]", "").Split(',') 
        |> Array.map (fun n -> System.Int32.Parse(n))

    let stringFold (proc:char -> string) (s:string)=
        String.Concat(s.Select(proc).ToArray())

    let foldProc (c:char) =
        if (int c) >= 128 then
            String.Format(@"\u{0:x4}", int c)
        else
            c.ToString()

    let escapeToAscii (s:string) = s |> stringFold foldProc

    type ProperCaseCamelCasePropertyNamesResolver() =
        inherit Serialization.DefaultContractResolver ()
        override x.ResolveDictionaryKey s = s
    
    let toJson o =
        JsonConvert.SerializeObject(o, 
            new JsonSerializerSettings(ContractResolver = new ProperCaseCamelCasePropertyNamesResolver(), TypeNameHandling=TypeNameHandling.All))
            
    let encode = toJson

    let toType<'a> (jsonObjectList:string) =
        let convert value = JsonConvert.DeserializeObject<'a>(value)
        let value token = (token:JToken).["value"].ToString()
        let values = Seq.map (value >> convert)
        let parse  = JObject.Parse
        let children jo = (jo:JObject).["items"].Children()

        sprintf "{items:%s}" jsonObjectList 
        |> (parse >> children >> values)




type ConsumerInstance =
    { Group : string
      Name : string
      BaseUri : string }
    with
        static member Default = 
            { Group = "UnknownGroup"
              Name = "UnknownConsumer"
              BaseUri = "/" }


type ConfluenceAdapter(rootUrl) =

    member x.rootUrl = rootUrl

    member x.url ending = rootUrl + "/" + ending

    member x.request(url, ?headers, ?httpMethod, ?body) : Result<string,string> =
        try
            Http.RequestString(url, ?httpMethod = httpMethod, ?headers = headers, ?body = body)
            |> cleanJson
            |> Success
        with
            | :? System.Net.WebException as ex -> Error ex.Message

    member x.getUrl = x.url >> x.request

    member x.query(path)  = x.request (x.url path)

    member x.toConsumerInstance (consumerGroup:string) (createResult:Result<string,string>):Result<ConsumerInstance,string> =
        match createResult with
        | Success json -> 
            let jo = JObject.Parse(json)
            Success { Name = string jo.["instance_id"]; BaseUri = string jo.["base_uri"]; Group = consumerGroup }
        | Error msg -> Error msg


(* Message topic creation *)

/// Kafka proxy proxy
type Kafka(rootUrl) =
    inherit ConfluenceAdapter(rootUrl)

    let rand = System.Random()

    member x.listTopics()  = "topics" |> x.getUrl

    member x.topics() = x.listTopics() |> splitJsonArray

    member x.topicMetadata(topic:string) = sprintf "topics/%s" topic |> x.getUrl

    member x.topicPartitionMetadata(topic:string) = sprintf "topics/%s/partitions" topic |> x.getUrl

    member x.schemaPolicy() = "topics/_schemas" |> x.getUrl

    member x.publishMessage(topic, msg) =
        x.request
          ( x.url "topics/" + topic,
            headers = [ "Content-Type", "application/vnd.kafka.avro.v1+json" ],
            body = TextRequest msg )

    // TODO: Sanitize consumer 'name' input for consumer URL creation
    member x.createConsumer (consumerGroup:string) =
        x.request
          ( x.url (sprintf "consumers/%s" consumerGroup),
             headers = [ "Content-Type", "application/vnd.kafka.avro.v1+json" ],
             body = TextRequest (sprintf """{"format": "avro", "auto.offset.reset": "smallest"}""" ))
        |> x.toConsumerInstance consumerGroup

    member x.deleteConsumer(consumer:ConsumerInstance) =
         x.request
          ( x.url (sprintf "consumers/%s/instances/%s" consumer.Group consumer.Name),
            httpMethod = "DELETE")

    member x.deleteConsumerInstance(consumer:ConsumerInstance) =
        x.deleteConsumer(consumer)

    member x.consume(consumer:ConsumerInstance, topic:string) =
        x.request
         ( x.url (sprintf "consumers/%s/instances/%s/topics/%s" consumer.Group consumer.Name topic),
           httpMethod = "GET",
           headers = [ "Accept", "application/vnd.kafka.avro.v1+json" ])
            
    member x.consume<'a> (consumer:ConsumerInstance, topic:string) =
        x.consume(consumer, topic) |> sval |> toType<'a>

    member x.consumeAll(topic:string) =
        let consumerName = sprintf "consumeall_%05i_" (rand.Next(1, 99999))
        x.createConsumer(consumerName) 
        |> sval
        |> (fun consumer ->
            printfn "%O"consumer
            let consumedData = x.consume(consumer, topic)
            x.deleteConsumer(consumer) |> ignore
            consumedData)

    member x.consumeAll<'a> (topic:string) =
        x.consumeAll topic |> sval |> toType<'a>

    member x.produceVersionedMessage schemaId (message:'a) =
        let messageJson = message |> toJson |> escapeToAscii
        
        sprintf """{"value_schema_id": "%i", "records": [{"value": %s}]}""" schemaId messageJson

    member x.publishVersionedMessage(topic, schemaId, (message:'a)) =
        let versionedMessage = x.produceVersionedMessage schemaId message
        printf "publishing: %s\r\n" versionedMessage
        x.publishMessage(topic, versionedMessage)
      


(* Schema manipulation  *)
type SchemaRegistry(rootUrl) =
    inherit ConfluenceAdapter(rootUrl)
    
    let extractSchema (json:string) =
        let schemaStart = json.IndexOf("schema\":\"") + 9
        let schema = json.Substring(schemaStart, json.Length - schemaStart - 2)
        JToken.Parse(schema).ToString()

    let identity (rawSchemaResult:Result<string,string>) = 
        match rawSchemaResult with
        | Success rawJson -> 
            let schemaInfoEnd = rawJson.IndexOf(",\"schema\":\"")
            let json = rawJson.Substring(0, schemaInfoEnd) + "}"
            let info = JObject.Parse(json)
            let id = Int32.Parse(info.["id"].ToString())
            let version = (Int32.Parse(info.["version"].ToString()))
            id, version
        | Error msg -> failwith msg

    let justSchema = function
        | Success json -> json |> extractSchema
        | Error msg -> failwith msg
        

    member x.registerSchema(subject, schema) = 
        x.request
          ( x.url (sprintf "subjects/%s/versions" subject),
            headers = [ "Content-Type", "application/vnd.schemaregistry.v1+json" ],
            httpMethod = "POST",
            body = TextRequest schema)

    member x.subjects() = "subjects" |> x.getUrl |> splitJsonArray

    member x.subjectStatus(subject:string) =
        sprintf "subjects/%s/versions/latest" subject |> x.getUrl
        
    member x.subjectVersions(subject:string) = 
        let getVersions = sprintf "subjects/%s/versions" subject |> x.getUrl
        match getVersions with
        | Success versionArray -> versionArray |> splitIntArray
        | Error msg -> failwith msg
            
    member x.rawSchema(subject:string, version:int) = 
        sprintf "subjects/%s/versions/%i" subject version |> x.getUrl

    member x.latestSchema(subject:string) = 
        x.subjectStatus(subject) |> justSchema
                
    member x.schema(subject:string) = 
        x.latestSchema(subject)

    member x.schema(schemaId:int) = 
        sprintf "schemas/ids/%i" schemaId |> x.getUrl |> justSchema
    
    member x.schema(subject:string, version:int) = 
        x.rawSchema(subject, version) |> justSchema
        
    member private x.latest (subject:string) = x.subjectStatus(subject)

    member x.latestSchemaVersion(subject:string) = x.latest(subject) |> identity |> snd

    member x.latestSchemaId(subject:string) = x.latest(subject) |> identity |> fst
    
    member x.schemaId(subject:string, version:int) = x.rawSchema(subject, version) |> identity |> fst

    member x.listVersions() =
        match x.subjects() with
        | Success subjects ->
            for s in subjects do
                let v = x.subjectVersions(s)
                printf "%s %A\r\n" s v
        | Error msg -> failwith msg



(* Schema Registry persistence *)
module SchemaPersistence =

    let private forEachVersion filter func (registry:SchemaRegistry) = 
        match registry.subjects() with
        | Success subjects ->
            for s in subjects |> Array.filter filter do
                let v = registry.subjectVersions(s)
                func registry s v
        | Error msg -> failwith msg

    let writeSchemas toFolder (registry:SchemaRegistry) = 

        let topicFilter (s:string) = not (s.StartsWith("logs") || s.StartsWith("coyote"))

        let writeSchema (registry:SchemaRegistry) subject versions =
            for v in versions do
                let schema = registry.schema(subject, v)
                let targetDir = sprintf "%s\\%s" toFolder subject
                let fileTarget = sprintf "%s\\%s.v%04i.avsc" targetDir subject v
                Directory.CreateDirectory(targetDir) |> ignore
                File.WriteAllText(fileTarget, schema)

        registry |> forEachVersion topicFilter writeSchema

    let loadSchemas fromFolder (registry:SchemaRegistry) =
        
        let schemata = Directory.GetFiles(fromFolder, "*.avsc", SearchOption.AllDirectories)

        let subjectFromFile fileName = 
            let name = Path.GetFileNameWithoutExtension(fileName)
            let split = name.IndexOf(".v")
            let subject = name.Substring(0, split)
            let version = Int32.Parse(name.Substring(split + 2))
            subject, version

        for schemaFile in schemata do
            let subject, version = subjectFromFile schemaFile
            let schemaContent = JObject.Parse(File.ReadAllText(schemaFile)).ToString(Formatting.None)
            let schema = sprintf """{"schema": %s}""" (JsonConvert.ToString(schemaContent))
            printfn "registering: %s %s" subject schema
            match registry.registerSchema(subject, schema) with
            | Success s -> ignore
            | Error msg -> failwith msg
            |> ignore


module Streams =
    
    let messageLog () = new Kafka("http://BBORG-AARCOMY:8082")
    let schemaRegistry () = new SchemaRegistry("http://BBORG-AARCOMY:8081")


