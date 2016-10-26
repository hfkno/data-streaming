

(*


	Basic interop between .Net solutions and Kafka

		Kafka
		Zookeeper

    REST API documentation @ http://docs.confluent.io/3.0.0/kafka-rest/docs/intro.html

*)

#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
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
let toJson o =
    JsonConvert.SerializeObject(o, 
        new JsonSerializerSettings(ContractResolver = new Serialization.CamelCasePropertyNamesContractResolver())
    )
let encode = toJson


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



(* Message topic creation *)
type Kafka(rootUrl) =
    inherit ConfluenceAdapter(rootUrl)

    member x.listTopics()  = "topics" |> x.getUrl

    member x.topics() = x.listTopics() |> splitJsonArray

    member x.topicMetadata(topic:string) = sprintf "topics/%s" topic |> x.getUrl

    member x.topicPartitionMetadata(topic:string) = sprintf "topics/%s/partitions" topic |> x.getUrl

    member x.schemaPolicy() = "topics/_schemas" |> x.getUrl

    member x.produceMessage(topic, msg) =
        x.request
          ( x.url "topics/" + topic,
            headers = [ "Content-Type", "application/vnd.kafka.avro.v1+json" ],
            body = TextRequest msg )

    // TODO: Sanitize consumer 'name' input for consumer URL creation
    member x.createConsumer(consumerName:string) =
         x.request
          ( x.url (sprintf "consumers/my_avro_consumer"),
            headers = [ "Content-Type", "application/vnd.kafka.avro.v1+json" ],
            body = TextRequest (sprintf """{"name": "%s", "format": "avro", "auto.offset.reset": "smallest"}""" consumerName))

    member x.deleteConsumer(consumerName:string) =
         x.request
          ( x.url (sprintf "consumers/my_avro_consumer/instances/%s" consumerName),
            httpMethod = "DELETE")

    member x.consume(consumerName:string, topic:string) =
        x.request
         ( x.url (sprintf "consumers/my_avro_consumer/instances/%s/topics/%s" consumerName topic),
           httpMethod = "GET",
           headers = [ "Accept", "application/vnd.kafka.avro.v1+json" ])

    member x.produceVersionedMessage(topic, schemaId, (message:'a)) =
        let messageJson = message |> toJson
        let versionedMessage = sprintf """{"value_schema_id": "%i", "records": [{"value": %s}]}""" schemaId messageJson
        printf "publishing: %s\r\n" versionedMessage
        x.produceMessage(topic, versionedMessage)

        
let k = new Kafka("http://localhost:8082")
k.listTopics()
k.schemaPolicy()
k.topics()
k.topicMetadata("basictest2")
k.topicPartitionMetadata("basictest2")

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
    match k.produceMessage(topic, postData) with
    | Success msg -> printf "%s" msg |> ignore
    | Error msg -> failwith msg

// Post a message with rolling data - known schema
for i in 91 .. 99 do
    let postData = dataId.Replace("testUser", sprintf "testUser%i" i)
    k.produceMessage(topic, postData) |> ignore

// Init consumer
let consumerName = "ze_test_consumer"
k.createConsumer(consumerName)

// Read updated rolling data
match k.consume(consumerName, "ad_user") with
| Success str -> printf "%s" str |> ignore
| Error msg -> printf "%s" msg |> ignore

// Cleanup
k.deleteConsumer(consumerName)






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


type User = 
    {
        Id : int
        Name : string
        Title : string
        Email : string
        Department : string
    }

let atest = { Id = 0; Name = "Amber Allad"; Title="Junior Janitor"; Email = "aa@hfk.no"; Department = "Sanitation"; }
let schemaId = r.latestSchemaId("ad_user-value")
k.produceVersionedMessage("ad_user-value", schemaId, atest)