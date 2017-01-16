


#load "fsharp_avro_generation.fsx"
#load "streaming.fsx"

open Fsharp_avro_generation
open Streaming
open System.Linq
open Newtonsoft.Json
open Newtonsoft.Json.Linq

// Push a sched to kafka
        // independent sched which pushes jobs when they are read
        

// poll the job queue: dispatch jobs and start them in background threads

// job puller can implement websockets and use webhooks to push data...


type Integration = 
    { Id : string
      Execute : unit -> unit }

 







// use schemastring with kafka interop to get a new schema and start sending F# messages through the new pipeline


// Type to register
type JobStatus = 
    {
        Status : string // Complete | Progress | Started
        Message : string
        Created : string
    }


let schema = SchemaGenerator.generateSchema<JobStatus> "hfk.utility.test.orchestration"
let schemaJson = sprintf """{"schema": "%s"}""" (schema.Schema.Replace("\"", "\\\""))

let r = Streams.schemaRegistry()
r.registerSchema(schema.Name.ToLower()  + "_telemetry-value", schemaJson)

let utcNow = System.DateTime.UtcNow.ToString("s") + "Z" 


let getReg() =
    { Status = "Complete"
      Message = "Registration"
      Created = utcNow }


let k = Streams.messageLog()

for i in 1 .. 10000 do
    printfn "%i" i
    printf "%A" 
        (k.publishVersionedMessage("hfk.utility.test.orchestration.JobStatus_telemetry", 6, getReg()))
        



type PublicationInfo = 
    { Name : string
      SchemaId : int }

let pubInfo<'a> (name:string) (purpose:string) = 
    let schema = SchemaGenerator.generateSchema<'a> name
    let schemaJson = sprintf """{"schema": "%s"}""" (schema.Schema.Replace("\"", "\\\""))

    let name = schema.Name.ToLower()  + "_" + purpose
    let json = r.registerSchema(name + "-value", schemaJson) |> sval
    let id = JObject.Parse(json).Properties().First().Value.Value<int>()
    { Name = name; SchemaId = id }


type RandomTelemetry = 
    { Message : string
      Value : int }      
    
let info = pubInfo<RandomTelemetry> "hfk.utility.test.orchestration" "telemetry" 


let doTelemFill () =
    async {
        for i in 1 .. 10000 do
            printfn "%i" i
            printfn "%A" 
                (k.publishVersionedMessage(info.Name, info.SchemaId, {Message="I can publish!!";Value=i}))
        }

[ doTelemFill(); doTelemFill(); doTelemFill(); doTelemFill()]
|> Async.Parallel
|> Async.RunSynchronously
