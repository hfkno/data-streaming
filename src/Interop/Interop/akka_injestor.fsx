

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


type ``Visma Leverandør Data``= CsvProvider<"data\suppliers.csv"> // CSV schemas can be hard coded into scripts

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
            printf "Publishing\r\n"
            printf "%s" "rawr"
            printf "Webservice got %A\r\n" details
             // recieved supplier #%i: %s (%s, %s)" 
               //     details.Id details.Name details.ContactName details.ContactTitle

let handleContent content = 
    Amesto.WebService.publish content


let reader (mailbox:Actor<System.Uri>) =

    let handler = spawn mailbox "handler" <| actorOf handleContent

    let rec loop() = actor {
        let! uri = mailbox.Receive()
        let suppliers = ``Visma Leverandør Data``.Load(uri.LocalPath)
        for supplier in suppliers.Rows do
            handler <! 
                { Id = supplier.SupplierID 
                  Name = supplier.CompanyName
                  ContactName = supplier.ContactName
                  ContactTitle = supplier.ContactTitle } 
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

    let reader = spawn mailbox "reader" reader
    let fileCreated uri =
        // FileSystemWatcher alerts of file creation while files are still locked
        let readDelay = new TimeSpan(0,0,0,0,300)
        scheduler.ScheduleTellOnce(readDelay, reader, uri)
    
    // subscribe to incoming file system events - send them to consoleWriter
    let subscription = 
        fsw.Created 
        |> Observable.map (fun file -> new System.Uri(file.FullPath))
        |> Observable.filter (fun uri -> uri.LocalPath.EndsWith(".csv"))
        |> Observable.subscribe fileCreated
        
    mailbox.Defer <| fun () -> 
        subscription.Dispose()
        fsw.Dispose()

    let rec loop () = actor {
        let! msg = mailbox.Receive()
        return! loop()
    }
    loop ()


let system = ActorSystem.Create("fileWatcher-system")

let manager = spawn system "manager" <| actorOf2 (fun mailbox filePath ->
    let managerName = "observer-" + Uri.EscapeDataString(filePath)
    let watcher = fileWatcher system.Scheduler filePath
    spawn mailbox managerName watcher |> ignore)

manager <! __SOURCE_DIRECTORY__ + "\\test\\"


system.Terminate()







