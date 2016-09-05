

(*


	Basic interop between .Net solutions and Kafka

		Kafka
		Zookeeper

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
        | Exists s -> str.Replace("[", "").Replace("]", "").Replace("\"", "").Split(',')
        | _ -> [||]
    | Error msg -> [| msg |]



type ConfluenceAdapter(rootUrl) =
    member x.url ending = rootUrl + "/" + ending
    member x.request(url, ?headers, ?body) : Result<string,string> = 
        try 
            Http.RequestString(url, ?headers = headers, ?body = body) 
            |> cleanJson 
            |> Success
        with 
            | :? System.Net.WebException as ex -> Error ex.Message
    member x.list(topic)  = x.request (x.url topic)


(* Message topic creation *)
type Kafka(rootUrl) =
    inherit ConfluenceAdapter(rootUrl)
    member x.listTopics()  = x.request (x.url "topics")
    member x.topics() = x.listTopics() |> splitJsonArray
    member x.listSchemas() = x.request (x.url "topics/_schemas")
    member x.produceMessage(topic, msg) = 
        x.request 
          ( x.url "topics/" + topic,
            headers = [ "Content-Type", "application/vnd.kafka.avro.v1+json" ], 
            body = TextRequest msg )


let k = new Kafka("http://localhost:8082")

k.list("topics/")
k.listTopics()
k.listSchemas()
k.topics()

// produding a message with Avro metadata embedded
k.produceMessage("basictest2", """{"value_schema": "{\"type\": \"record\", \"name\": \"User\", \"fields\": [{\"name\": \"name\", \"type\": \"string\"}]}", "records": [{"value": {"name": "testUser"}}]}""")






(* Schema manipulation  *)
type Registry(rootUrl) =
    inherit ConfluenceAdapter(rootUrl)
    member x.listTopics()  = x.request (x.url "subjects")
    member x.topics() = x.listTopics() |> splitJsonArray
    member x.schema(id:int) = x.request (x.url (sprintf "schemas/ids/%i" id))


let r = new Registry("http://localhost:8081")


r.list("schemas/ids/1")
r.list("subjects")
r.list("subjects/basictest2-value/versions")
r.list("subjects/basictest-value/versions/1")
r.listTopics()
r.topics()
r.schema(1)