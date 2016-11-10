

(*

    Basic Akka.Net file reader -> webservice agency

*)


#r "../../packages/Akka/lib/net45/Akka.dll"
#r "../../packages/Akka.FSharp/lib/net45/Akka.FSharp.dll"
#r "../../packages/Akka.Serialization.Wire/lib/net45/Akka.Serialization.Wire.dll"
#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#r "../../packages/FSPowerPack.Linq.Community/Lib/net40/FSharp.PowerPack.Linq.dll"
#r "../../packages/System.Collections.Immutable/lib/portable-net45+win8+wp8+wpa81/System.Collections.Immutable.dll"
#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "../../packages/Serilog/lib/net46/Serilog.dll"
#r "../../packages/Serilog.Sinks.Literate/lib/net45/Serilog.Sinks.Literate.dll"
#r "../../packages/Akka.Logger.Serilog/lib/net45/Akka.Logger.Serilog.dll"
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


#load "AkkaExtensions.fs"
open Interop.Lifecycle

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
                .MinimumLevel.Information()
                .WriteTo.LiterateConsole()
                .Enrich.FromLogContext() 
                .CreateLogger();

let retrySupervision = 
    Strategy.OneForOne 
        ((fun e -> 
            match e with 
            | :? System.IO.IOException -> Directive.Restart
            | _ -> Directive.Stop
            ), 
        retries=1, 
        timeout=TimeSpan.FromSeconds(3.0))
                
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
    let subscription =
        fsw.Created 
            |> Observable.map (fun file -> new System.Uri(file.FullPath))
            |> Observable.filter filter
            |> Observable.subscribe handler
    fsw, subscription



// Consumer

type SupplierDetails = 
    { 
        Name : string
        OrgNr : string
        Address : string
        PostNr : string
        CountryCode : string
        Email : string
        PhoneNr : string
        MobilePhoneNr : string
        FaxNr : string
        ResKontroNr : string
    }

module Amesto =
    module WebService =
        let publish(details:SupplierDetails) = 
            Log.Information("Publishing {@details}", details)



// Producer

[<Literal>]
let ``Visma Leverandør CSV Schema`` = "Name (string), OrgNr (string), Address (string), PostNr (string), CountryCode (string), \
                                       Email (string), PhoneNr (string), MobilePhoneNr (string), FaxNr (string), ResKontroNr (string)"
type ``Visma Leverandør Data``= CsvProvider<Schema=``Visma Leverandør CSV Schema``, Separators = ";", HasHeaders = false>

let publishContent content = 
    Amesto.WebService.publish content

let publishRows publisher (uri:Uri) =
    use suppliers = ``Visma Leverandør Data``.Load(uri.LocalPath) 
    for supplier in suppliers.Rows do
        publisher <! 
              { Name = supplier.Name
                OrgNr = supplier.OrgNr
                Address = supplier.Address
                PostNr = supplier.PostNr
                CountryCode = supplier.CountryCode
                Email = supplier.Email
                PhoneNr = supplier.PhoneNr
                MobilePhoneNr = supplier.MobilePhoneNr
                FaxNr = supplier.FaxNr
                ResKontroNr = supplier.ResKontroNr }
                

type FileReader(publisher) =
    inherit Actor() 

    override x.OnReceive message =
        match message with
        | :? Uri as uri ->
            Log.Information("Attempting to read file {uri} ({Self})", uri, x.Self)
            uri |> publishRows publisher
        | _ -> failwith "unknown format"   

    override x.PreRestart(e, message) =
        match e with
        | :? System.IO.IOException ->
            let context = FileReader.Context
            Log.Information("RESTARTING FILE READER")
            context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(0.2), context.Self, message)
        | _ -> ()
        

let folderWatcher path (mailbox:Actor<_>) =    

    let onlyCsv (uri:Uri) = uri.LocalPath.EndsWith(".csv")

    let publisher = spawn mailbox "publisher" <| actorOf publishContent // add superviser here?
    let props = [| publisher :> obj |]
    let fileReader = mailbox.ActorOf(Props(typedefof<FileReader>, retrySupervision, props), name="filereader" )
    
    let read (uri:Uri) = fileReader <! uri
   
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
    spawn mailbox folderWatcherName watcher |> ignore

let config = ConfigurationFactory.ParseString("akka { loglevel=INFO,  loggers=[\"Akka.Logger.Serilog.SerilogLogger, Akka.Logger.Serilog\"]}")
let system = ActorSystem.Create("fileWatcher-system", config)
let manager = spawn system "manager" (actorOf2 integrationManager) 

manager <! __SOURCE_DIRECTORY__ + "\\test\\"



system.Terminate()