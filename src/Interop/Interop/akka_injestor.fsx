

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
            printf "Webservice has: %i %s %s %s\r\n" details.Id details.Name details.ContactName details.ContactTitle




type ``Visma Leverandør Data``= CsvProvider<"data\suppliers.csv"> // CSV schemas can be hard coded into scripts...

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

type ReadFile = | ReadFile of attempts:int * Uri

let fileIsOpen (uri:Uri) =
    try
        use stream = File.Open(uri.LocalPath, FileMode.Open,  FileAccess.Read, FileShare.ReadWrite)
        stream.Close()
        false
    with
        | :? System.IO.IOException -> true

let fileReader (mailbox:Actor<ReadFile>) =

    let publisher = spawn mailbox "handler" <| actorOf publishContent

    let rec loop() = actor {
        let! ReadFile(attempts, uri) = mailbox.Receive()

        let delayRead attempts uri =
            let readDelay = new TimeSpan(0,0,0,0,300)
            mailbox.Context.System.Scheduler.ScheduleTellOnce(readDelay, mailbox.Self, ReadFile(1, uri))

        if fileIsOpen uri then
            uri |> delayRead (attempts + 1)
        else 
            publisher |> publishRows uri
        return! loop()
    }
    loop()

let fileWatcher filePath (mailbox:Actor<_>) =    
    let fsw = new FileSystemWatcher(
                        Path = filePath, 
                        Filter = "*.*",
                        EnableRaisingEvents = true, 
                        NotifyFilter = (NotifyFilters.FileName ||| NotifyFilters.LastWrite ||| NotifyFilters.LastAccess ||| NotifyFilters.CreationTime ||| NotifyFilters.DirectoryName)
                        )

    let reader = spawn mailbox "filereader" fileReader

    let eventSubscription = 
        fsw.Created 
        |> Observable.map (fun file -> new System.Uri(file.FullPath))
        |> Observable.filter (fun uri -> uri.LocalPath.EndsWith(".csv"))
        |> Observable.subscribe (fun uri -> reader <! ReadFile(0, uri))
            
    mailbox.Defer <| fun () -> 
        eventSubscription.Dispose()
        fsw.Dispose()

    let rec loop () = actor {
        let! msg = mailbox.Receive()
        return! loop()
    }
    loop ()


let fileWatchingManager (mailbox:Actor<string>) filePath = 
    let managerName = "observer-" + Uri.EscapeDataString(filePath)
    let watcher = fileWatcher filePath
    spawn mailbox managerName watcher |> ignore


let system = ActorSystem.Create("fileWatcher-system")
let manager = spawn system "manager" (actorOf2 fileWatchingManager)

manager <! __SOURCE_DIRECTORY__ + "\\test\\"


system.Terminate()
