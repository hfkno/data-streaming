

(*


	Basic interop between .Net solutions and Kafka

		Kafka
		Zookeeper

*)

#r "../packages/FSharp.Data.2.3.2/lib/net40/FSharp.Data.dll" 
open FSharp.Data
open System.IO


// Basic connectivity
//
Http.RequestString("http://localhost:8082/topics/")
Http.RequestString("http://localhost:8082/topics/_schemas")


// Post to a new topic with an included schema
//
// curl -X POST -H "Content-Type: application/vnd.kafka.avro.v1+json" \
//       --data '{"value_schema": "{\"type\": \"record\", \"name\": \"User\", \"fields\": [{\"name\": \"name\", \"type\": \"string\"}]}", "records": [{"value": {"name": "testUser"}}]}' \
//       "http://localhost:8082/topics/avrotest"
Http.RequestString 
  ( "http://localhost:8082/topics/basictest", 
    headers = [ "Content-Type", "application/vnd.kafka.avro.v1+json" ], 
    body = TextRequest """{"value_schema": "{\"type\": \"record\", \"name\": \"User\", \"fields\": [{\"name\": \"name\", \"type\": \"string\"}]}", "records": [{"value": {"name": "testUser"}}]}"""  )





// Wrapper functionality

let cleanJson (json:string) = json.Replace("\\\"", "\"")


type ConfluenceAdapter(rootUrl) =
    member x.url ending = rootUrl + "/" + ending
    member x.request(url, ?headers, ?body) = 
        Http.RequestString(url, ?headers = headers, ?body = body) |> cleanJson



type Kafka(rootUrl) =
    inherit ConfluenceAdapter(rootUrl)

    member x.list(topic)  = x.request (x.url topic)
    member x.listTopics()  = x.request (x.url "topics")
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
// produding a message with Avro metadata embedded
k.produceMessage("basictest2", """{"value_schema": "{\"type\": \"record\", \"name\": \"User\", \"fields\": [{\"name\": \"name\", \"type\": \"string\"}]}", "records": [{"value": {"name": "testUser"}}]}""")




// Schema manipulation

type Registry(rootUrl) =
    inherit ConfluenceAdapter(rootUrl)
    member x.list(topic)  = x.request (x.url topic)
    member x.listTopics()  = x.request (x.url "schemas/ids/0")


let r = new Registry("http://localhost:8081")

let rr = r.list("schemas/ids/1")
r.list("subjects")
let z = r.list("subjects/basictest2-value/versions")
let s = r.list("subjects/basictest-value/versions/1") .cleanJson.cleanJson.cleanJson..

"DFd" + rr + "zz"

String.length(rr);

rr.ToCharArray()
rr.Replace(@"\", "|")


let rz = r.list("schemas/ids/1").Replace("\\\"", "\"")