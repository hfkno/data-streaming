


#load "fsharp_avro_generation.fsx"
#load "streaming.fsx"

open Fsharp_avro_generation
open Streaming
open System.Linq
open System.Collections.Generic
open Newtonsoft.Json
open Newtonsoft.Json.Linq

// Push a sched to kafka
        // independent sched which pushes jobs when they are read
        

// poll the job queue: dispatch jobs and start them in background threads

// job puller can implement websockets and use webhooks to push data...


type Integration = 
    { Id : string
      Execute : unit -> unit }

 
 
let utcNow = System.DateTime.UtcNow.ToString("s") + "Z"         

type PublicationInfo = 
    { Name : string
      SchemaId : int }

let pubInfo<'a> (name:string) (purpose:string) = 
    let schema = SchemaGenerator.generateSchema<'a> name
    let schemaJson = sprintf """{"schema": "%s"}""" (schema.Schema.Replace("\"", "\\\""))

    let name = schema.Name  + "_" + purpose
    let json = Streams.schemaRegistry().registerSchema(name + "-value", schemaJson) |> sval
    let id = JObject.Parse(json).Properties().First().Value.Value<int>()
    { Name = name; SchemaId = id }





type JobStatus = 
    { Status : string // Complete | Progress | Started
      Message : string
      Created : string }


type RandomTelemetry =  
    { Message : string
      Value : int }      

let jnfo = pubInfo<JobStatus> "hfk.utility.test.orchestration" "telemetry"     
let info = pubInfo<RandomTelemetry> "hfk.utility.test.orchestration" "telemetry" 





// Subscribe to messages and read FSHarp records on the other side XD

let k = Streams.messageLog()
let c = k.createConsumer("randomObserver") |> sval
let ret = k.consume(c, "hfk.utility.test.orchestration.JobStatus_telemetry")
k.deleteConsumer(c)


let rawrRet = ret |> sval





// parse message return in JSON
let jsonRet = """[{"key":null,"value":{"Status":"Complete","Message":"Registration","Created":"2017-01-16T11:33:13Z"},"partition":0,"offset":0},{"key":null,"value":{"Status":"Complete","Message":"Registration","Created":"2017-01-16T11:33:13Z"},"partition":0,"offset":1},{"key":null,"value":{"Status":"Complete","Message":"Registration","Created":"2017-01-16T11:33:13Z"},"partition":0,"offset":2},{"key":null,"value":{"Status":"Complete","Message":"Registration","Created":"2017-01-16T11:33:13Z"},"partition":0,"offset":3}]"""
let jsonRet2 = """[{"key":null,"value":{"Status":"Complete","Message":"Registration","Created":"2017-01-16T11:33:13Z"},"partition":0,"offset":0]"""

JObject.Parse("""{items: [{"key":null,"value":""},{"key":null,"value":""}]}""")


let jo = JObject.Parse(sprintf "{items:%s}" jsonRet)
let d = jo.["items"].[0].["value"].["Status"].Value<string>()

type TestR = { One : string; Two: int}
let moo = [{One = "yo"; Two = 3};{One="hey";Two=4}]
let tjson2 = JsonConvert.SerializeObject(moo)
let ffff = JsonConvert.DeserializeObject<IEnumerable<TestR>>(tjson2)
ffff

let toType<'a> (jsonObjectList:string) =
    
    let value token = (token:JToken).["value"].ToString()
    let convert value = JsonConvert.DeserializeObject<'a>(value) 
    let parse  = JObject.Parse
    let items jo = (jo:JObject).["items"].Children()
    let jo = JObject.Parse(sprintf "{items:%s}" jsonObjectList)
    let tokens = jo.["items"].Children()


    let tokens =
        (sprintf "{items:%s}" jsonObjectList) 
        |> parse
        |> items



    [ for token in tokens do 
        yield token |> value |> convert
        //yield JsonConvert.DeserializeObject<'a>(token.["value"].ToString()) 
        ]


jsonRet |> toType<JobStatus>
                
"""[{"key":null,"value":""},{"key":null,"value":""}]""" |> toType<TestR>       


// Setup a schedule
// Kickoff the integrations using the Int type defined above
// Publish success to Kafka
// Start publishing data through there
    // employees
    // suppliers
    // Data transfers







let simpleFill() =
    let getReg() =
        { Status = "Complete"
          Message = "Registration"
          Created = utcNow }


    let k = Streams.messageLog()

    for i in 1 .. 10000 do
        printfn "%i" i
        printf "%A" 
            (k.publishVersionedMessage("hfk.utility.test.orchestration.JobStatus_telemetry", 6, getReg()))


let massFill () = 
    let doTelemFill () =
        let k = Streams.messageLog()
        async {
            for i in 1 .. 100000 do
                printfn "%i" i
                printfn "%A" 
                    (k.publishVersionedMessage(info.Name, info.SchemaId, {Message="I can publish!!";Value=i}) |> sval)
            }

    [ doTelemFill(); 
      doTelemFill(); 
      doTelemFill(); 
      doTelemFill(); 
      doTelemFill(); 
      doTelemFill(); 
      doTelemFill(); ]
    |> Async.Parallel
    |> Async.RunSynchronously
