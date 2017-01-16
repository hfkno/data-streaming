


#load "fsharp_avro_generation.fsx"
#load "streaming.fsx"

open Fsharp_avro_generation
open Streaming


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




