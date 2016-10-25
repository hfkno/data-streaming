

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


type User = 
    {
        Id : int
        Name : string
        Title : string
        Email : string
        Department : string
    }

let atest = { Id = 0; Name = "Amber Allad"; Title="Junior Janitor"; Email = "aa@hfk.no"; Department = "Sanitation"  }


JsonConvert.SerializeObject(atest)


let k = new Kafka("http://localhost:8082")
k.listTopics()
k.schemaPolicy()
k.topics()
k.topicMetadata("basictest2")
k.topicPartitionMetadata("basictest2")


// produding a message with Avro metadata embedded
let valueSchema = """{ \"type\": \"record\", \"name\": \"User\", \"fields\": [ { \"name\": \"name\", \"type\": \"string\" }, { \"name\": \"nameo\", \"type\": \"string\", \"default\" : \"ddd\" } ] }"""
let records = """{"value": {"Name": "testUser", "Nameo": "hi"}}"""
let data = sprintf """{"value_schema": "%s", "records": [%s]}""" valueSchema records
let valueSchemaId = 1
let dataId = sprintf """{"value_schema_id": "%i", "records": [%s]}""" valueSchemaId records
let topic = "testing_6"

// Post a message with rolling data
for i in 81 .. 89 do
    let postData = data.Replace("testUser", sprintf "testUser%i" i)
    k.produceMessage(topic, postData) |> ignore

// Post a message with rolling data - known schema
for i in 91 .. 99 do
    let postData = dataId.Replace("testUser", sprintf "testUser%i" i)
    k.produceMessage(topic, postData) |> ignore

// Init consumer
let consumerName = "ze_test_consumer"
k.createConsumer(consumerName)

// Read updated rolling data
match k.consume(consumerName, "testing_1") with
| Success str -> printf "%s" str |> ignore
| Error msg -> printf "%s" msg |> ignore

// Cleanup
k.deleteConsumer(consumerName)





(* Schema manipulation  *)
type SchemaRegistry(rootUrl) =
    inherit ConfluenceAdapter(rootUrl)

    member x.subjects() = x.request (x.url "subjects") |> splitJsonArray

    member x.subjectVersions(subject:string) = sprintf "subjects/%s/versions" subject |> x.getUrl

    member x.schema(subject:string, id:int) = sprintf "subjects/%s/versions/%i" subject id |> x.getUrl

    member x.schemaById(id:int) = sprintf "schemas/ids/%i" id |> x.getUrl

    member x.latestSchema(subject:string) = sprintf "subjects/%s/versions/latest" subject |> x.getUrl    

    member x.registerSchema(subject, schema) = 
        x.request
          ( x.url (sprintf "subjects/%s/versions" subject),
            headers = [ "Content-Type", "application/vnd.schemaregistry.v1+json" ],
            httpMethod = "POST",
            body = TextRequest schema)

    member x.latestSchemaVersion(subject:string) =
        match x.subjectVersions(subject) with
        | Success versionArr -> 
            (versionArr |> splitIntArray).Last()
        | Error msg -> failwith msg

    member x.listVersions() =
        match x.subjects() with
        | Success subjects ->
            for s in subjects do
                match x.subjectVersions(s) with
                | Success v -> printf "%s %s\r\n" s v
                | Error msg -> printf "%s" msg
        | Error msg -> failwith msg

    member x.forEachVersion filter func = 
        match x.subjects() with
        | Success subjects ->
            for s in subjects |> Array.filter filter do
                match x.subjectVersions(s) with
                | Success v -> func x s v
                | Error msg -> failwith msg
        | Error msg -> failwith msg

    member x.writeSchemas toFolder = 

        let topicFilter (s:string) = not (s.StartsWith("logs") || s.StartsWith("coyote"))

        let writeSchema (registry:SchemaRegistry) subject versions =
            for v in  versions |> splitIntArray do
                match registry.schema(subject, v) with
                | Success wrappedSchema -> 
                    let schemaStart = wrappedSchema.IndexOf("schema\":\"") + 9
                    let schema = wrappedSchema.Substring(schemaStart, wrappedSchema.Length - schemaStart - 2)
                    let targetDir = sprintf "%s\\%s" toFolder subject
                    let fileTarget = sprintf "%s\\%s.v%04i.avsc" targetDir subject v
                    Directory.CreateDirectory(targetDir) |> ignore
                    let jt = JToken.Parse(schema)
                    File.WriteAllText(fileTarget, jt.ToString())
                | Error msg -> failwith msg

        x.forEachVersion topicFilter writeSchema

    member x.loadSchemas fromFolder =
        
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
            match x.registerSchema(subject, schema) with
            | Success s -> ignore
            | Error msg -> failwith msg
            |> ignore



let r = new SchemaRegistry("http://localhost:8081")
r.loadSchemas("C:\\proj\\poc\\models")



r.writeSchemas("C:\\proj\\test")

r.subjects()
r.registerSchema("randotesto3" + "-value", """{"schema": "{\"type\": \"record\", \"name\": \"User\", \"fields\": [ { \"name\": \"name\", \"type\": \"string\" }, { \"name\": \"nameo\", \"type\": \"string\", \"default\" : \"ddd\" } ] }"}""")
r.subjectVersions("randotesto3-value")
r.subjectVersions("aduser-value")
r.schema("randotesto3-value", 1)
r.latestSchema("randotesto3-value")
r.latestSchemaVersion("randotesto3-value")
r.listVersions()


// TODO: Upgrade to a new schema with a breaking change...
