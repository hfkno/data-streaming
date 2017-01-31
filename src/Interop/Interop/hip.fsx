 

#I "../../packages/"
#r "Quartz/lib/net40-client/Quartz.dll"
#r "Common.Logging/lib/net40/Common.Logging.dll"
#r "Common.Logging.Core/lib/net40/Common.Logging.Core.dll"
#load "fsharp_avro_generation.fsx"
#load "streaming.fsx"

open Fsharp_avro_generation
open Streaming
open System.IO
open System.Net
open System.Linq
open System.Collections.Generic
open Newtonsoft.Json
open Newtonsoft.Json.Linq



// Push a sched to kafka
        // independent sched which pushes jobs when they are read
        

// poll the job queue: integration runners should subscribe to some metadata to count runs, or whatever...

// job puller can implement websockets and use webhooks to push data...






// Setup a schedule
// Kickoff the integrations using the Integration type defined below
// Publish success to Kafka
// Start publishing data through there
    // employees
    // suppliers
    // Data transfers



open Quartz
open Quartz.Impl

let schedulerFactory = StdSchedulerFactory()
let scheduler = schedulerFactory.GetScheduler()
scheduler.Start()

type Job () =
    interface IJob with
        member x.Execute(context: IJobExecutionContext) =
            printfn "%s" (System.DateTime.Now.ToString())

let job = JobBuilder.Create<Job>().Build()

let trigger =
    TriggerBuilder.Create()
        .WithSimpleSchedule(fun x ->
            x.WithIntervalInSeconds(1).RepeatForever() |> ignore)
        .Build()

let t2 () = 
    TriggerBuilder.Create()
        .WithCronSchedule("cron cron cron")
        .Build()

let sch = scheduler.ScheduleJob(job, trigger) // |> ignore

for s in scheduler.GetJobGroupNames() do
    for key in scheduler.GetJobKeys(Matchers.GroupMatcher.GroupStartsWith(s)) do
        printfn "%s:%s" (key.Group) (key.Name)

scheduler.Shutdown(true)

// Job definition example
// let job = 
//     { new IJob with
//          member this.Execute ...
//        }





// Basic types
type JobStatus = 
    { Status : string // Complete | Progress | Started
      Message : string
      Created : string }


type RandomTelemetry =  
    { Message : string
      Value : int }     


type Integration = 
    { Id : string
      Execute : unit -> unit }

 

module SchemaLookup =
    let private ids = 
            dict [ 
                typeof<JobStatus>, 4
                typeof<RandomTelemetry>, 5
            ]
    let find (t:'a) =
        ids.Item(t)


type ConnectionStatus = Active | Inactive

[<AutoOpen>]
module Utility =

    let canConnectTo (uri:string) =
        let conn () = 
            async {
                let req = WebRequest.CreateHttp(uri)
                let! response = req.AsyncGetResponse()
                return (response :?> HttpWebResponse).StatusCode }
        try
            Async.RunSynchronously(conn()) = HttpStatusCode.OK
        with    
            | _ -> false

    let fileExists = File.Exists

type KafkaProxy() =

    let k = Streams.messageLog()
    let logFile = @"C:\\temp\\kafka_msg_log.txt"
    let schemaId (forMsg :'a) = SchemaLookup.find typeof<'a>

    member x.pubToKafka (topic, message:'a) =
        k.publishVersionedMessage(topic, message |> schemaId, message)

    member x.pubToFile (topic, message:'a) =
        let msgLine = sprintf "%s|%i|%s" topic (message |> schemaId) (message |> toJson)
        File.AppendAllLines(logFile, [ msgLine ])    


    member x.pubFileToKafka fileName =
        for line in File.ReadAllLines(fileName) do
            let chunk = line.Split('|')
            let topic, schema, mesageJson = chunk.[0], chunk.[1], chunk.[2]


            failwith "THIS FUNCTION IS INCOMPLETE, THE FILE WILL NOT WRITE TO KAFKA"


            ()

    member x.publishVersionedMessage(topic, (message:'a)) =
        match canConnectTo(k.rootUrl) with
        | true -> 
            if logFile |> fileExists then
                () // pub every log message
            x.pubToKafka (topic, message) |> ignore
        | _ -> x.pubToFile (topic, message)
       
        
let line = "rawr|unf|nurrrrrr"          
let chunk = line.Split('|')
let topic, schema, mesageJson = chunk.[0], chunk.[1], chunk.[2]
//canConnectTo("http://localhost:3030/")


// Message Generation
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



// setup messages for publishing
let jnfo = pubInfo<JobStatus> "hfk.utility.test.orchestration" "telemetry"     
let info = pubInfo<RandomTelemetry> "hfk.utility.test.orchestration" "telemetry" 


// message consumption
let k = Streams.messageLog()
let consumer = k.createConsumer("randomObserver") |> sval
let tret = k.consume<JobStatus>(consumer, "hfk.utility.test.orchestration.JobStatus_telemetry")
k.deleteConsumer(consumer)
tret



// message generation
let simpleFill() =
    let getReg() =
        { Status = "Complete"
          Message = "Registration"
          Created = utcNow }


    let k = Streams.messageLog()

    for i in 1 .. 10000 do
        printfn "%i" i
        printf "%A" 
            (k.publishVersionedMessage("hfk.utility.test.orchestration.JobStatus_telemetry", (SchemaLookup.find typeof<JobStatus>), getReg()))


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
