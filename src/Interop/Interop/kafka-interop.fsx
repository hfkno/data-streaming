

(*


	Basic interop between .Net solutions and Kafka

		Kafka
		Zookeeper

    REST API documentation @ http://docs.confluent.io/3.0.0/kafka-rest/docs/intro.html


    Docker startup:

    docker run --rm -it `
            -p 2181:2181 -p 3030:3030 -p 8081:8081 `
            -p 8082:8082 -p 8083:8083 -p 9092:9092 `
            landoop/fast-data-dev

*)

#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#load "ad_vismae_integration.fsx"
open FSharp.Data
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open System.IO
open System.Linq



(*


    Wrapper functionality


*)
type Result<'TSuccess,'TError> =
     | Success of 'TSuccess
     | Error of 'TError

// Success value
let inline sval (res:Result<'a,'b>) = match res with | Success a -> a | Error msg -> failwith msg


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
        new JsonSerializerSettings(ContractResolver = new ProperCaseCamelCasePropertyNamesResolver()))

let encode = toJson




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
//
//         x.request
//          ( x.url (sprintf "consumers/%s" consumerGroup),
//            headers = [ "Content-Type", "application/vnd.kafka.avro.v1+json" ],
//            body = TextRequest (""""{format": "avro", "auto.offset.reset": "smallest"}""" ))
        |> x.toConsumerInstance consumerGroup

    member x.deleteConsumer(consumerGroup:string, consumerName:string) =
         x.request
          ( x.url (sprintf "consumers/%s/instances/%s" consumerGroup consumerName),
            httpMethod = "DELETE")

    member x.deleteConsumerInstance(consumer:ConsumerInstance) =
        x.deleteConsumer(consumer.Group, consumer.Name)

    member x.consume(consumerName:string, topic:string) =
        x.request
         ( x.url (sprintf "consumers/my_avro_consumer/instances/%s/topics/%s" consumerName topic),
           httpMethod = "GET",
           headers = [ "Accept", "application/vnd.kafka.avro.v1+json" ])

    member x.consumeAll(topic:string) =
        let consumerName = sprintf "consumeall_%05i_" (rand.Next(1, 99999))
        x.createConsumer(consumerName) 
        |> sval
        |> (fun consumer ->
            printfn "%O"consumer
            let consumedData = x.consume(consumerName, topic)
            x.deleteConsumer(consumer.Group, consumer.Name) |> sval |> ignore
            consumedData)

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




// TODO: need to check all consumer stuff..

            
let k = new Kafka("http://localhost:8082")
k.createConsumer("herro")
k.listTopics()
k.schemaPolicy()
k.topics()

// produding a message with Avro metadata embedded
let valueSchema = """{\"type\": \"record\", \"name\": \"User\", \"fields\": [{\"name\": \"name\", \"type\": \"string\"}, { \"name\": \"nameo\", \"type\": \"string\", \"default\" : \"ddd\" } ]}"""
let records = """{"value": {"name": "testUser", "nameo": "erro"}}"""
let data = sprintf """{"value_schema": "%s", "records": [%s]}""" valueSchema records
let valueSchemaId = 1
let dataId = sprintf """{"value_schema_id": "%i", "records": [%s]}""" valueSchemaId records
let topic = "testing_8"

// Post a message with rolling data
for i in 21 .. 29 do
    let postData = data.Replace("testUser", sprintf "testUser%i" i)
    k.publishMessage(topic, postData) |> sval |> printf "%s" 

// Post a message with rolling data - known schema
for i in 91 .. 99 do
    let postData = dataId.Replace("testUser", sprintf "testUser%i" i)
    k.publishMessage(topic, postData) |> ignore

// Init consumer
let consumerGroup = "ze_test_consumer"
let consumer =  k.createConsumer(consumerGroup) |> sval 

// Read updated rolling data
match k.consume(consumerGroup, "ad_user") with
| Success str -> printf "%s" str |> ignore
| Error msg -> printf "%s" msg |> ignore

// Cleanup
k.deleteConsumer(consumer.Group, consumer.Name)


let r = new SchemaRegistry("http://localhost:8081")

r.registerSchema("randotesto3" + "-value", """{"schema": "{\"type\": \"record\", \"name\": \"User\", \"fields\": [ { \"name\": \"name\", \"type\": \"string\" }, { \"name\": \"nameo\", \"type\": \"string\", \"default\" : \"ddd\" } ] }"}""")
r.subjects()
r.subjectVersions("activedirectoryuser-value")
r.latestSchemaVersion("ad_user-value")
r.latestSchemaId("ad_user-value")
r.schema(17)
r.schema("ad_user-value")
r.schema("randotesto3-value", 1)
r.latestSchema("ad_user-value")
r.listVersions()


r |> SchemaPersistence.loadSchemas  "C:\\proj\\poc\\models"
r |> SchemaPersistence.writeSchemas "C:\\proj\\test"

// TODO: Upgrade to a new schema with a breaking change...

let json = """{"instance_id":"ze_test_consumer","base_uri":"http://localhost:8082/consumers/my_avro_consumer/instances/ze_test_consumer"}"""

let jObj = JObject.Parse(json)
string jObj.["instance_id"]
jObj.Properties() |> Seq.toList
string (jObj.GetValue("instance_id"))


open Ad_vismae_integration.ActiveDirectory
let adUserTest = 
    { EmployeeId = "0" 
      DisplayName = "Tøfusting Messæging"
      Account = "TESMESS"
      Email = "testmess@example.no"
      IsActive = true
      WorkPhone = ""
      MobilePhone = ""
      FirstName = "Testing"
      LastName = "" }


let schemaId = r.latestSchemaId("ad_user-value")
k.produceVersionedMessage 5 adUserTest
k.publishVersionedMessage("ad_user-value", schemaId, adUserTest)

let users = Ad_vismae_integration.ActiveDirectory.users() |> Seq.toList

let results =
    [ for u in users do yield k.publishVersionedMessage("ad_user-value", schemaId,u) ]

for r in results do
    printf "%A\r\n" r


k.consumeAll("ad_user-value")


#time
// Init consumer
let adConsumer = k.createConsumer("ad_consumer") |> sval
// Read updated rolling data
match k.consume(adConsumer.Name, "ad_user-value") with
| Success str -> printf "%s" str |> ignore
| Error msg -> printf "%s" msg |> ignore
// Cleanup
k.deleteConsumerInstance(adConsumer)
#time  // Real: 00:00:02.187, CPU: 00:00:00.031, GC gen0: 2, gen1: 1, gen2: 0



#time 

Ad_vismae_integration.ActiveDirectory.users() |> Seq.toList

#time  // Real: 00:01:19.456, CPU: 00:00:10.718, GC gen0: 21, gen1: 3, gen2: 0



#time 

let caRet = k.consumeAll("ad_user-value")

#time // Real: 00:00:01.182, CPU: 00:00:00.031, GC gen0: 1, gen1: 0, gen2: 0
