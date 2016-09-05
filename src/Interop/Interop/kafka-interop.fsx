

(*


	Basic interop between .Net solutions and Kafka

		Kafka
		Zookeeper

    REST API documentation @ http://docs.confluent.io/3.0.0/kafka-rest/docs/intro.html

*)

#r "../packages/FSharp.Data.2.3.2/lib/net40/FSharp.Data.dll" 
open FSharp.Data
open System.IO


(* Basic connectivity *)
Http.RequestString("http://localhost:8082/topics/")
Http.RequestString("http://localhost:8082/topics/_schemas")

(*
   Post to a new topic with an included schema

   curl -X POST -H "Content-Type: application/vnd.kafka.avro.v1+json" \
        --data '{"value_schema": "{\"type\": \"record\", \"name\": \"User\", \"fields\": [{\"name\": \"name\", \"type\": \"string\"}]}", "records": [{"value": {"name": "testUser"}}]}' \
        "http://localhost:8082/topics/avrotest"
*)
Http.RequestString 
  ( "http://localhost:8082/topics/basictest", 
    headers = [ "Content-Type", "application/vnd.kafka.avro.v1+json" ], 
    body = TextRequest """{"value_schema": "{\"type\": \"record\", \"name\": \"User\", \"fields\": [{\"name\": \"name\", \"type\": \"string\"}]}", "records": [{"value": {"name": "testUser"}}]}"""  )   


   
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
    member x.listTopics()  = x.request (x.url "topics")
    member x.topics() = x.listTopics() |> splitJsonArray
    member x.schemaPolicy() = x.request (x.url "topics/_schemas")
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
           headers = [ "Content-Type", "application/vnd.kafka.avro.v1+json" ], 
           body = TextRequest (sprintf """{"name": "%s", "format": "avro", "auto.offset.reset": "smallest"}""" consumerName)) 



let k = new Kafka("http://localhost:8082")

k.listTopics()
k.schemaPolicy()
k.topics()

// produding a message with Avro metadata embedded
let valueSchema = """{\"type\": \"record\", \"name\": \"User\", \"fields\": [{\"name\": \"name\", \"type\": \"string\"}]}""" 
let records = """{"value": {"name": "testUser"}}"""
let data = sprintf """{"value_schema": "%s", "records": [%s]}""" valueSchema records
let topic = "test3"

// Post a message with rolling data
for i in 1 .. 40 do
    let postData = data.Replace("er", sprintf "er%i" i)
    k.produceMessage(valueSchema, postData) |> ignore



// Consume a message and show rolling data
(*
    curl -X POST -H "Content-Type: application/vnd.kafka.v1+json" \
          --data '{"name": "my_consumer_instance", "format": "avro", "auto.offset.reset": "smallest"}' \
          http://localhost:8082/consumers/my_avro_consumer

    curl -X GET -H "Accept: application/vnd.kafka.avro.v1+json" \
          http://localhost:8082/consumers/my_avro_consumer/instances/my_consumer_instance/topics/avrotest

    curl -X DELETE \
          http://localhost:8082/consumers/my_avro_consumer/instances/my_consumer_instance
*)
k.





// Upgrade to a new schema with a breaking change...





(* Schema manipulation  *)
type Registry(rootUrl) =
    inherit ConfluenceAdapter(rootUrl)
    member x.subjects() = x.request (x.url "subjects") |> splitJsonArray
    member x.rawSchema(id:int) = x.request (x.url (sprintf "schemas/ids/%i" id))
    member x.subjectVersions(subject:string) = sprintf "subjects/%s/versions" subject |> x.getUrl
    member x.schema(subject:string, id:int) = sprintf "subjects/%s/versions/%i" subject id |> x.getUrl
    member x.listVersions() =
        match x.subjects() with
        | Success subjects -> 
            for s in subjects do
                match x.subjectVersions(s) with
                | Success v -> printf "%s %s\r\n" s v
                | Error msg -> printf "%s" msg
        | Error msg -> 
            printf "%s" msg


let r = new Registry("http://localhost:8081")
r.subjects()
r.subjectVersions("basictest2-value")
r.schema("basictest2-value", 1)
r.listVersions()



