

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

#I "../../packages/"
#r "Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#load "ad_vismae_integration.fsx"
#load "streaming.fsx"

open Streaming


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


open Newtonsoft.Json

open Newtonsoft.Json.Linq
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
