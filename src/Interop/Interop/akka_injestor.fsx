

(*

    Basic Akka.Net file reader -> webservice agency

*)


#r "../../packages/Akka/lib/net45/Akka.dll"
#r "../../packages/Akka.FSharp/lib/net45/Akka.FSharp.dll"
#r "../../packages/Akka.DI.Autofac/lib/net45/Akka.DI.Autofac.dll"
#r "../../packages/Akka.Serialization.Wire/lib/net45/Akka.Serialization.Wire.dll"
#r "../../packages/System.Collections.Immutable/lib/portable-net45+win8+wp8+wpa81/System.Collections.Immutable.dll"
#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../../packages/FSPowerPack.Core.Community/Lib/net40/FSharp.PowerPack.dll"
#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"

open System
open System.IO
open Akka
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open FSharp.Data


type SupplierDetails = 
    { 
        Id : int
        Name : string
        ContactName : string
        ContactTitle : string   
    }

module Amesto =
    module WebService =
        let publish(details:SupplierDetails) = 
            printf "Webservice has: %i %s %s %s\r\n" 
                   details.Id details.Name details.ContactName details.ContactTitle




type ``Visma Leverandør Data``= CsvProvider<"data\suppliers.csv"> // CSV schemas can be hard coded into scripts

let publishContent content = 
    Amesto.WebService.publish content

let publishRows (uri:Uri) publisher =
    use suppliers = ``Visma Leverandør Data``.Load(uri.LocalPath) 
    for supplier in suppliers.Rows do
        publisher <! 
              { Id = supplier.SupplierID 
                Name = supplier.CompanyName
                ContactName = supplier.ContactName
                ContactTitle = supplier.ContactTitle }

let fileReader (mailbox:Actor<System.Uri>) =

    let publisher = spawn mailbox "handler" <| actorOf publishContent

    let rec loop() = actor {
        let! uri = mailbox.Receive()
        publisher |> publishRows uri 
        return! loop()
    }
    loop()

let fileWatcher (scheduler:ITellScheduler) filePath (mailbox:Actor<_>) =    
    let fsw = new FileSystemWatcher(
                        Path = filePath, 
                        Filter = "*.*",
                        EnableRaisingEvents = true, 
                        NotifyFilter = (NotifyFilters.FileName ||| NotifyFilters.LastWrite ||| NotifyFilters.LastAccess ||| NotifyFilters.CreationTime ||| NotifyFilters.DirectoryName)
                        )

    let reader = spawn mailbox "filereader" fileReader
    let fileCreated uri =
        let readDelay = new TimeSpan(0,0,0,0,300)
        scheduler.ScheduleTellOnce(readDelay, reader, uri) // FSW sends create event while the file is still locked
    
    let eventSubscription = 
        fsw.Created 
        |> Observable.map (fun file -> new System.Uri(file.FullPath))
        |> Observable.filter (fun uri -> uri.LocalPath.EndsWith(".csv"))
        |> Observable.subscribe fileCreated
        
    mailbox.Defer <| fun () -> 
        eventSubscription.Dispose()
        fsw.Dispose()

    let rec loop () = actor {
        let! msg = mailbox.Receive()
        return! loop()
    }
    loop ()


// refactring> let fileWatchingMaanger

let system = ActorSystem.Create("fileWatcher-system")

let manager = 
    spawn system "manager" 
    <| actorOf2 (fun mailbox filePath ->
        let managerName = "observer-" + Uri.EscapeDataString(filePath)
        let watcher = fileWatcher system.Scheduler filePath
        spawn mailbox managerName watcher 
        |> ignore)

manager <! __SOURCE_DIRECTORY__ + "\\test\\"


system.Terminate()


// TODO: the file watching logic is brittle - it needs to wait an
//       unspecified amount of time to check the file is unlocked (during slow batch jobs and file gen, f. ex)