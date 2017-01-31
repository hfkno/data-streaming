

#I "../../packages/"
#r "Quartz/lib/net40-client/Quartz.dll"
#r "Common.Logging/lib/net40/Common.Logging.dll"
#r "Common.Logging.Core/lib/net40/Common.Logging.Core.dll"
#load "ad_vismae_integration.fsx"

open System
open Quartz
open Quartz.Impl


[<AutoOpen>]
module Utility =

    type ITrigger with 
        /// Gets the next time the trigger will fire, regardless of scheduler status
        member x.GetNextFireTime() =
            x.GetFireTimeAfter(DateTimeOffset.Now |> Nullable)
             .Value
             .ToLocalTime()
             .ToString()


[<AutoOpen>]
module Triggers = 

    let everySecond = 
        TriggerBuilder.Create()
            .WithSimpleSchedule(fun x ->
                x.WithIntervalInSeconds(1)
                 .RepeatForever() |> ignore)
            .Build()

    let every30min = 
        TriggerBuilder.Create()
            .WithSimpleSchedule(fun x ->
                x.WithIntervalInMinutes(30)
                 .RepeatForever() |> ignore)
            .Build()


[<AutoOpen>]
module Jobs = 

    type ``AD to Visma E Synch`` () = 
        interface IJob with
            member x.Execute(context: IJobExecutionContext) =
                printfn "Sycnhing Active Directory at: %s" (System.DateTime.Now.ToUniversalTime().ToString())
                Ad_vismae_integration.doFullUpdate()

module Schedule =

    let masterSchedule =
        [ JobBuilder.Create<``AD to Visma E Synch``>(), every30min ]


module Scheduler =

    open Schedule

    let private getScheduler () = StdSchedulerFactory().GetScheduler()
    let mutable scheduler = getScheduler()

    let schedule (schedule:(JobBuilder * ITrigger) list) (scheduler:IScheduler) =
        for job, trigger in schedule do
            scheduler.ScheduleJob(job.Build(), trigger) |> ignore

    let setupSchedule () = 
        scheduler <- getScheduler()
        scheduler |> schedule masterSchedule

    let start () = 
        scheduler.Start()
        scheduler.ResumeAll()
    let stop () = scheduler.PauseAll()
    let shutdown () = scheduler.Shutdown()
    let nextFires (schedule:(JobBuilder * ITrigger) list) =
        for job, trigger in schedule do
            printfn "job %s next firing at %s" (trigger.Description) (trigger.GetNextFireTime())




module Test =

    let doTest () =
        Scheduler.setupSchedule()
        Scheduler.start()
        Scheduler.nextFires (Schedule.masterSchedule)
        Scheduler.stop()
        Scheduler.shutdown()

//
//type Job () =
//    interface IJob with
//        member x.Execute(context: IJobExecutionContext) =
//            printfn "Here is the time: %s" (System.DateTime.Now.ToUniversalTime().ToString())
//
//let job = JobBuilder.Create<Job>().Build()
//
//let trigger =
//    TriggerBuilder.Create()
//        .WithSimpleSchedule(fun x ->
//            x.WithIntervalInSeconds(1).RepeatForever() |> ignore)
//        .Build()
//
//let t2 = 
//    TriggerBuilder.Create()
//        .WithCronSchedule("	0 0/1 * 1/1 * ? *") //* * * * * ?")
//        .Build()
//
//let sch = scheduler.ScheduleJob(job, trigger) // |> ignore
//
//for s in scheduler.GetJobGroupNames() do
//    for key in scheduler.GetJobKeys(Matchers.GroupMatcher.GroupStartsWith(s)) do
//        printfn "%s:%s" (key.Group) (key.Name)
//
//
//printfn "%s" (trigger.GetNextFireTime()) // .GetFireTimeAfter(DateTimeOffset.Now |> Nullable).Value.ToLocalTime().ToString())
//
//
//printfn "%s" (trigger.GetNextFireTimeUtc().ToString())
//printfn "%s" (trigger.GetNextFireTimeUtc().Value.ToLocalTime().ToString())
//
//
//
//
//for j in scheduler.GetCurrentlyExecutingJobs() do
//    printfn "%s %s" (j.JobDetail.Description) (j.NextFireTimeUtc.ToString())
//
//scheduler.Shutdown(true)

// Job definition example
// let job = 
//     { new IJob with
//          member this.Execute ...
//        }


// Setup a schedule
// Run the Visma import every xxx minutes
// Verify
// Launch proper sched






    



