

(*

    Basic Akka.Net file reader -> webservice agency

*)


#r "../../packages/Akka/lib/net45/Akka.dll"
#r "../../packages/Akka.FSharp/lib/net45/Akka.FSharp.dll"
#r "../../packages/Akka.Serialization.Wire/lib/net45/Akka.Serialization.Wire.dll"

#r "../../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#r "../../packages/FSharp.Data.TypeProviders/lib/net40/FSharp.Data.TypeProviders.dll"
#r "System.ServiceModel"
#r "System.Runtime.Serialization"

#r "../../packages/FSPowerPack.Linq.Community/Lib/net40/FSharp.PowerPack.Linq.dll"
#r "../../packages/System.Collections.Immutable/lib/portable-net45+win8+wp8+wpa81/System.Collections.Immutable.dll"
#r "../../packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"

#r "../../packages/Serilog/lib/net46/Serilog.dll"
#r "../../packages/Serilog.Sinks.Literate/lib/net45/Serilog.Sinks.Literate.dll"
#r "../../packages/Akka.Logger.Serilog/lib/net45/Akka.Logger.Serilog.dll"
#r "../../packages/Destructurama.FSharp/lib/portable-net45+win+wpa81+wp80+MonoAndroid10+MonoTouch10/Destructurama.FSharp.dll"


open System
open System.IO
open System.ServiceModel
open System.Linq
open System.Text.RegularExpressions

open Akka
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

open FSharp.Data
open FSharp.Data.TypeProviders
open FSharp.Linq

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
                .MinimumLevel.Debug()
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
        retries=5, 
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




// Contract

type SupplierDetails = 
    { 
        Name : string
        OrgNr : string
        BillingAddress : string
        PostNr : string
        CountryCode : string
        Email : string
        PhoneNr : string
        MobilePhoneNr : string
        FaxNr : string
        ResKontroNr : string
    }



// Producer

[<Literal>]
let ``Visma Leverandør CSV Schema`` = "Name (string), OrgNr (string), BillingAddress (string), PostNr (string), CountryCode (string), \
                                       Email (string), PhoneNr (string), MobilePhoneNr (string), FaxNr (string), ResKontroNr (string)"
type ``Visma Leverandør Data``= CsvProvider<Schema=``Visma Leverandør CSV Schema``, Separators = ";", HasHeaders = false>

let publishRows (uri:Uri) (publish : SupplierDetails -> Unit) =
    use suppliers = ``Visma Leverandør Data``.Load(uri.LocalPath) 
    for supplier in suppliers.Rows do
        publish  
              { Name = supplier.Name
                OrgNr = supplier.OrgNr
                BillingAddress = supplier.BillingAddress
                PostNr = supplier.PostNr
                CountryCode = supplier.CountryCode
                Email = supplier.Email
                PhoneNr = supplier.PhoneNr
                MobilePhoneNr = supplier.MobilePhoneNr
                FaxNr = supplier.FaxNr
                ResKontroNr = supplier.ResKontroNr }
                





// Consumer

module Amesto =
    module WebService =
        let publish(details:SupplierDetails) = 
            Log.Information("Publishing {@details}", details)


let publishContent content = 
    Amesto.WebService.publish content



[<Literal>]
let amestoServiceAddress = "http://hfk-www02-t.ad.hfk.no/Avantra/Customer/Hordaland/Service2013/actor.asmx?WSDL"
let supplierRegister = 1
let largeBinding = new BasicHttpBinding(MaxBufferSize = Int32.MaxValue, MaxReceivedMessageSize = int64(Int32.MaxValue))

type AmestoService = WsdlService<amestoServiceAddress>
let amesto = new AmestoService.ServiceTypes.ActorServiceSoapClient(largeBinding, new EndpointAddress(amestoServiceAddress))
let amestoSuppliers = amesto.GetActors(supplierRegister)



let cleanPostnr (nr:string) = 
    if String.IsNullOrWhiteSpace(nr) then
        "0000" 
    else
        sprintf "%04i" (Convert.ToInt32(nr))
let cleanName (name:string) = Regex.Replace(name, " (A/S|AS|ASA|SA|A\.S\.)$" , "")
let hasId (supplier:AmestoService.ServiceTypes.www.avantra.se.atc.service._2013.ActorD) = 
    not <| String.IsNullOrWhiteSpace(supplier.CompanyRegistrationNumber)





let testFile = new Uri(__SOURCE_DIRECTORY__ + "\\test\\amest_export_test.csv")
//publishRows testFile (fun d -> printfn "%A" d)


let vismaSuppliers = ``Visma Leverandør Data``.Load(testFile.LocalPath)
let vismaSuppliersGrouped = query {
    for supplier in vismaSuppliers.Rows do
    groupBy (supplier.OrgNr)
}

let amestoSuppliersGrouped = query {
    for supplier in amestoSuppliers do
    where (supplier |> hasId)
    groupBy (supplier.CompanyRegistrationNumber)
}


// Note: all this logic may be 1,000,000 times easier to write when/if the "ExternalKey" can be used for unique retrieval...

// TODO: match on external key...

for vismaSupplierGroup in (vismaSuppliersGrouped.Take(225)) do
    let existingSuppliers = query {
        for amestoSupplierGroup in amestoSuppliersGrouped do
        where (amestoSupplierGroup.Key = vismaSupplierGroup.Key)
        select (amestoSupplierGroup.ToArray())
    }
    
    let uploadSuppliers = vismaSupplierGroup.ToArray()
    let existing = existingSuppliers.ToArray() 

    for i in 0 .. (uploadSuppliers.Length - 1) do
        Log.Information("Reading upload nr {i} from len {Length}", i, uploadSuppliers.Length)
        let vismaSupp = uploadSuppliers.[i]

        if i < existing.Length then
            let amestoSupp = existing.[0].[i]
            if ((cleanPostnr amestoSupp.ZipCode) = (cleanPostnr vismaSupp.PostNr)) && ((cleanName amestoSupp.Name) = (cleanName vismaSupp.Name)) then
                Log.Information("Updating supplier '{Id}'", amestoSupp.SupplierNumber)
                amesto.SaveActorById((amestoSupp.Id.Value), "integration:true", vismaSupp.Name, vismaSupp.OrgNr, amestoSupp.SupplierNumber, amestoSupp.VisitAddress, vismaSupp.BillingAddress, (cleanPostnr vismaSupp.PostNr), amestoSupp.City, amestoSupp.Region, vismaSupp.CountryCode, vismaSupp.PhoneNr, vismaSupp.MobilePhoneNr, vismaSupp.FaxNr, vismaSupp.Email, amestoSupp.Url, amestoSupp.PlusGiro, amestoSupp.BankGiro, null) |> ignore
            else
                Log.Information("Creating new supplier '{Id}'", vismaSupp.ResKontroNr)
                amesto.SaveActorByExternalKey(vismaSupp.ResKontroNr,  "integration:true", vismaSupp.Name, vismaSupp.OrgNr, vismaSupp.ResKontroNr, vismaSupp.BillingAddress, vismaSupp.BillingAddress, (cleanPostnr vismaSupp.PostNr), "", "", vismaSupp.CountryCode, vismaSupp.PhoneNr, vismaSupp.MobilePhoneNr, vismaSupp.FaxNr, vismaSupp.Email, "", "", "", null) |> ignore
        else
            Log.Information("Creating new supplier '{Id}'", vismaSupp.ResKontroNr)
            amesto.SaveActorByExternalKey(vismaSupp.ResKontroNr,  "integration:true", vismaSupp.Name, vismaSupp.OrgNr, vismaSupp.ResKontroNr, vismaSupp.BillingAddress, vismaSupp.BillingAddress, (cleanPostnr vismaSupp.PostNr), "", "", vismaSupp.CountryCode, vismaSupp.PhoneNr, vismaSupp.MobilePhoneNr, vismaSupp.FaxNr, vismaSupp.Email, "", "", "", null) |> ignore
    ()

//    for supplier in g do
//        printfn "%-50O %-12O %-8O %O" supplier.Name supplier.CompanyRegistrationNumber supplier.ZipCode supplier.PostalAddress






//  ISSUES: 
//      Companies without "AS" - how to match?
            // Example
//      Companies with inconsistent naming 
            // Example (CLUE NORGE A/S) vs CLUE NORGE ASA
//      Companies with multiple addresse, same orgNr, and a different orgNr in Amesto (treat them as different?  Do this whole op by orgnumber to ensure valiid matches?)
            // Example: CRAMO AS - this should be uploaded twice...
//      Companies with mulitple addresses and a matching orgnr 
            // Example Coop Hordaland SA
//      Companies with multiple reskontro and multiple orgnr matches
            // Example Clas Ohlson AS            
//      Companies with multiple entries in Amesto, but only one in the import
            // Example CASIO SCANDANAVIA AS
//      Companies with multiple entries in import but only one in Amesto (w same orgnr)
            // Example CAPELLEN DAMM A
//      Companies with orgnrs that are poorly formatted
            // Example CADMUM AB 556341-7541     
//      Names with norwegian (gotta check)
//      Companies with multiple names and same orgnr
            // Example: CERALIA AS 910629085
            //          CEREALIA AS 910629085



// Orchestration

type FileReader(publisher) =
    inherit Actor() 

    override x.OnReceive message =
        match message with
        | :? Uri as uri ->
            Log.Information("Attempting to read file {uri} ({Uid})", uri, x.Self.Path.Uid)
            publishRows uri (fun details -> publisher <! details)
        | _ -> failwith "Unknown message format"   

    override x.PreRestart(e, message) =
        Log.Debug("Restarting file reader ({Uid})", x.Self.Path.ToStringWithUid())
        match e with
        | :? System.IO.IOException ->  
            Log.Debug("Rescheduling file read command to wait for unlocked file")
            let context = FileReader.Context            
            context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(0.2), context.Self, message)
        | _ -> ()
        

let folderWatcher path (mailbox:Actor<_>) =    

    let onlyCsv (uri:Uri) = uri.LocalPath.EndsWith(".csv")

    let publisher = spawn mailbox "publisher" <| actorOf publishContent
    let props = [| publisher :> obj |]
    let fileReader = mailbox.ActorOf(Props(typedefof<FileReader>, retrySupervision, props), name="filereader" )
    
    let read (uri:Uri) = fileReader <! uri
   
    let fsw, eventSubscription = fileEventSubscription onlyCsv read path
            
    mailbox.Defer <| fun () -> 
        eventSubscription.Dispose()
        fsw.Dispose()

    Log.Information("Watching folder '{path}'", path)
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