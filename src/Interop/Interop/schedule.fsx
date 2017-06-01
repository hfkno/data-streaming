

#I "../../packages/"
#r "Quartz/lib/net40-client/Quartz.dll"
#r "Common.Logging/lib/net40/Common.Logging.dll"
#r "Common.Logging.Core/lib/net40/Common.Logging.Core.dll"
#load "ad_vismae_integration.fsx"

open System
open Quartz
open Quartz.Impl
open Ad_vismae_integration

[<AutoOpen>]
module Utility =

    let localTimestamp (utc:Nullable<DateTimeOffset>) =
        if utc.HasValue then
            utc.Value.ToLocalTime().ToString()
        else
            "00.00.00 00:00:00"

    type ITrigger with 
        /// Gets the next time the trigger will fire, regardless of scheduler status
        member x.GetNextFireTime() =
            x.GetFireTimeAfter(DateTimeOffset.Now |> Nullable) |> localTimestamp

        /// Gets the next time the trigger will fire, regardless of scheduler status
        member x.GetLastFireTime() =
            x.GetPreviousFireTimeUtc() |> localTimestamp

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
                printfn "Synching Active Directory at: %s" (System.DateTime.Now.ToString())
                let results = Ad_vismae_integration.Test.doFullUpdate()
                for r in results |> List.filter(fun r -> match r with Error m -> true | _ -> false) do 
                    printfn "Error during synch: %A" r

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
    let lastFires (schedule:(JobBuilder * ITrigger) list) =
        for job, trigger in schedule do
            printfn "job %s last fired at %s" (trigger.Description) (trigger.GetLastFireTime())



module Test =

    let doTest () =
        Scheduler.setupSchedule()
        let fjob = Scheduler.scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.AnyGroup()) |> Seq.head
        Scheduler.scheduler.TriggerJob(fjob)
        Scheduler.start()
        Scheduler.nextFires (Schedule.masterSchedule)
        Scheduler.lastFires (Schedule.masterSchedule)
        Scheduler.stop()
        Scheduler.shutdown()










