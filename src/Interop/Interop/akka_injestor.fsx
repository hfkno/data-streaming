

(*

    Basic Akka.Net file reader -> webservice agency

*)

#r "../../packages/Akka/lib/net45/Akka.dll"
#r "../../packages/Akka.FSharp/lib/net45/Akka.FSharp.dll"
#r "../../packages/Akka.Serialization.Wire/lib/net45/Akka.Serialization.Wire.dll"
#r "../../packages/System.Collections.Immutable/lib/portable-net45+win8+wp8+wpa81/System.Collections.Immutable.dll"
#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#r "../../packages/Serilog/lib/net46/Serilog.dll"
#r "../../packages/Serilog.Sinks.Literate/lib/net45/Serilog.Sinks.Literate.dll"
#r "../../packages/Destructurama.FSharp/lib/portable-net45+win+wpa81+wp80+MonoAndroid10+MonoTouch10/Destructurama.FSharp.dll"


open System
open System.IO
open Akka
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open FSharp.Data
open Serilog
open Serilog.Configuration


// TODO:  unit testing...
//
//        Test strategy:
//          Create a test system
//          Observe a folder
//          Make changes to the folder
//          Check that publishing is sent
//
//          Test publishing...
//
//          Split testing... Test the management and observation, then test "reader <! ReadFile(0, uri)", then test locking


// Utility

do            
    Log.Logger <- LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.LiterateConsole()
                .Enrich.FromLogContext() 
                .CreateLogger();


let retrySupervision = Strategy.OneForOne ((fun e -> Log.Error(e, "Opening file"); Directive.Restart), retries=10, timeout=TimeSpan.FromSeconds(20.0))
                
let supervision = 
    Strategy.OneForOne (fun e ->
    match e with 
    | _ ->
        Log.Error(e, "Could not open locked file")
        Directive.Restart)

let fileIsOpen (uri:Uri) =
    try
        use stream = File.Open(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        stream.Close()
        false
    with
        | :? System.IO.IOException -> true

let fileEventSubscription filter handler path =
    let fsw = new FileSystemWatcher(
                        Path = path, 
                        Filter = "*.*",
                        EnableRaisingEvents = true, 
                        NotifyFilter = (NotifyFilters.FileName ||| NotifyFilters.LastWrite ||| NotifyFilters.LastAccess ||| NotifyFilters.CreationTime ||| NotifyFilters.DirectoryName)
                        )
    fsw, fsw.Created 
        |> Observable.map (fun file -> new System.Uri(file.FullPath))
        |> Observable.filter filter
        |> Observable.subscribe handler


// Consumer

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
            Log.Information("Publishing {@details}", details)
            //printf "Webservice has: %i %s %s %s\r\n" details.Id details.Name details.ContactName details.ContactTitle



// Producer

type ``Visma Leverandør Data``= CsvProvider<"data\suppliers.csv"> // CSV schemas can be hard coded into scripts...

let publishContent content = 
    Amesto.WebService.publish content

let publishRows publisher (uri:Uri) =
    use suppliers = ``Visma Leverandør Data``.Load(uri.LocalPath) 
    for supplier in suppliers.Rows do
        publisher <! 
              { Id = supplier.SupplierID 
                Name = supplier.CompanyName
                ContactName = supplier.ContactName
                ContactTitle = supplier.ContactTitle }

type ReadFile = | ReadFile of attempts:int * Uri

let fileReader (mailbox:Actor<ReadFile>) =

    let maxRetries, retryDelayMs = 10, 200

    let delayedRead (attempts:int) uri =
        Log.Information("Delaying read of '{uri}' after {attempts} attempts to open", uri, attempts)
        let readDelay = new TimeSpan(0,0,0,0,retryDelayMs)
        mailbox.Context.System.Scheduler.ScheduleTellOnce(readDelay, mailbox.Self, ReadFile(1, uri))

    let publisher = spawn mailbox "publisher" <| actorOf publishContent

    let rec loop() = actor {
        let! ReadFile(attempts, uri) = mailbox.Receive()
        Log.Information("Attempting to read file {uri}", uri)

        if attempts >= maxRetries then 
            raise (System.IO.IOException(sprintf "Could not open file '%s'." (uri.ToString())))

        if fileIsOpen uri then
            uri |> delayedRead (attempts + 1)
        else 
            uri |> publishRows publisher
        return! loop()
    }
    loop()


let folderWatcher path (mailbox:Actor<_>) =    

    let reader = spawn mailbox "filereader" fileReader
    let read (uri:Uri) = reader <! ReadFile(0, uri)
    let onlyCsv (uri:Uri) = uri.LocalPath.EndsWith(".csv")

    let fsw, eventSubscription = fileEventSubscription onlyCsv read path
            
    mailbox.Defer <| fun () -> 
        eventSubscription.Dispose()
        fsw.Dispose()

    let rec loop () = actor {
        let! msg = mailbox.Receive()
        return! loop()
    }
    loop ()


let integrationManager (mailbox:Actor<string>) path = 
    let folderWatcherName = "observer-" + Uri.EscapeDataString(path)
    let watcher = folderWatcher path
    spawnOpt mailbox folderWatcherName watcher [SupervisorStrategy(supervision)] |> ignore

let system = ActorSystem.Create("fileWatcher-system")
let manager = spawnOpt system "manager" (actorOf2 integrationManager) [SupervisorStrategy(supervision)]

manager <! __SOURCE_DIRECTORY__ + "\\test\\"



system.Terminate()
