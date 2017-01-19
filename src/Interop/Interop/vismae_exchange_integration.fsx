(**
Visma Enterprise -> Exchange Integration
========================================


Integration overview
-------------------

  * Retreive "WebMail" users from Visma Enterprise 
  * Set "SendOnBehalf" in Exhcnage for those users


Integration details
-------------------

The Exchange webservice is located at "https://mail.hfk.no/EWS/Exchange.asmx"

The user account used for this integration requires identity impersonation permission.
See: https://msdn.microsoft.com/en-us/library/bb204095.aspx

Powershell based solutions require heavy installs and difficult crendentials management.
The Exchange Web Service has been chosen instead.



Setting up the Exchange Shell on local Windows Machine 
------------------------------------------------------
From : https://technet.microsoft.com/en-us/library/dd335083(v=exchg.160).aspx

    Set-ExecutionPolicy RemoteSigned
    $UserCredential = Get-Credential

    mail.hfk.no
*)



// TODO: log error for users missing email addresses in Visma E
// TODO: Missing delegates on specific users: Hildegunn.Fischer@hfk.no, 6809:Tor.Oddvar.Sjovoll@hfk.no, 9137:Nevenka.Radic@hfk.no, 16088:Else.Vassenden@hfk.no, 16580:Kristoffer.Gilhus@hfk.no, 17333:Birthe.Haugen@hfk.no, 20531:Bente.Leivestad@hfk.no, 22032:Roald.Breistein@hfk.no, 22038:Hogne.Haktorson@hfk.no

#I "../../packages/"
#r "FAKE.Lib/lib/net451/FakeLib.dll"
#r "Microsoft.Exchange.WebServices/lib/40/Microsoft.Exchange.WebServices.dll"
#load "configuration.fsx"
#load "ad_vismae_integration.fsx"

open System
open System.Linq
open Microsoft.Exchange.WebServices.Data
open Ad_vismae_integration
open Configuration


let uname, pass, domain, delegateEmail, fakturaGroups
    = Configuration.ExchangeAdmin.UserName, 
      Configuration.ExchangeAdmin.Password, 
      Configuration.ExchangeAdmin.Domain, 
      "vismapost@hfk.no",
      [ "WEB_EHANDEL"; "WEB_FAKTURABEHANDLING"; "WEB_ØKONOMI"; "WEB_EORDRE" ]



module Exchange =

    let service = new ExchangeService(
                        Url = new Uri("https://mail.hfk.no/EWS/Exchange.asmx"), 
                        Credentials = new WebCredentials(uname, pass, domain))


    let impersonatedMailbox userEmail =
        service.ImpersonatedUserId <- new ImpersonatedUserId(ConnectingIdType.SmtpAddress, userEmail)
        new Mailbox(userEmail)

    let getDelegatesFor userEmail = 
        try
            service.GetDelegates(impersonatedMailbox userEmail, true).DelegateUserResponses
        with
            | :? Microsoft.Exchange.WebServices.Data.ServiceResponseException as e when e.Message.Contains("no mailbox associated") ->
                new Collections.ObjectModel.Collection<DelegateUserResponse>()
    

    let showDelegates (vismaId, userEmail) = 
        printfn "showing user : %i - %s"vismaId userEmail
        for d in getDelegatesFor userEmail do
            match d.DelegateUser with
            | null -> printfn "Got a null user back from a delegate of user %i:%s" vismaId userEmail
            | _    -> printfn "%s" d.DelegateUser.UserId.DisplayName


    let showAllDelegate (vismaId, userEmail) = 
        printfn "showing user : %i - %s"vismaId userEmail
        service.GetDelegates(impersonatedMailbox userEmail, true).DelegateUserResponses
        

    let hasDelegate (userEmail) (delegateEmail:string) =
        (getDelegatesFor userEmail)
            .Any(fun d -> 
                match d.DelegateUser with
                | null -> false
                | _ -> d.DelegateUser.UserId.PrimarySmtpAddress.ToLower() = delegateEmail.ToLower())

    let setDelegate (delegateEmail : string) (forUser : string) =
        let scope  = System.Nullable(MeetingRequestsDeliveryScope.DelegatesAndMe)
        try
            let delegateUser = new DelegateUser(delegateEmail)
            //delegateUser.Permissions  <- new DelegatePermissions 
            let ret = service.AddDelegates(impersonatedMailbox forUser, scope, delegateUser) |> Seq.head
            match ret.Result with
            | ServiceResult.Success -> Success (sprintf "'%s' got delegate '%s'" forUser delegateEmail)
            | _ -> Error (sprintf "'%s' did not get delegate '%s': '%s'" forUser delegateEmail ret.ErrorMessage)            
        with
            | :? Microsoft.Exchange.WebServices.Data.ServiceResponseException as e when e.Message.Contains("no mailbox associated") ->
                Error (sprintf "No mailbox associated with '%s'" forUser)
                

module VismaEnterprise =

    let fakturaUsers () =
        let allUsers = VismaEnterprise.UserService.users()
        let allGroups = VismaEnterprise.UserService.groups() |> Seq.toList

        let fakturaUserIds = set [ for g in allGroups do
                                       if fakturaGroups.Contains(g.Name) then
                                           for m in g.Members do yield m ]

        query {
            for u in allUsers do
            where (fakturaUserIds.Contains(u.VismaId) && (not <| String.IsNullOrWhiteSpace(u.Email)))
            sortBy u.VismaId
            select (u.VismaId, u.Email)
        } |> Seq.toList



module Integration =

    let private wrapAdUsers (adUsers:ActiveDirectory.User seq) =
        adUsers |> Seq.map(fun u -> 0, u.Email)


    let adUsers () = 
        ActiveDirectory.users () 
            |> Seq.filter (fun u -> u.Email |> exists) 
            |> wrapAdUsers


    let usersMissingDelegateFrom users delegateEmail =
        [ for (id, email) in users do
            printfn "Checking %s for delegates" email
            if not <| Exchange.hasDelegate email delegateEmail then 
                yield (id, email) ]
                
    let showUsersMissingDelegate users delegateEmail = 
        for (id, email) in usersMissingDelegateFrom users delegateEmail do
            printfn "\"%-40s\" - missing delegate" email

    let showEFakturaUsersMissingDelegate delegateEmail = 
        showUsersMissingDelegate (VismaEnterprise.fakturaUsers()) delegateEmail

    let showActiveDirectoryUsersMissingDelegate delegateEmail = 
        showUsersMissingDelegate (adUsers()) delegateEmail

    let setDelegate id email delegateEmail =
        printfn "Setting delegate for %i:%s" id email
        email |> Exchange.setDelegate delegateEmail

    let setDelegatesFor users delegateEmail =
        [ for (id, email) in users do 
            yield setDelegate id email delegateEmail ]
            
    let setDelegatesForEFakturaUsers delegateEmail =
        setDelegatesFor (VismaEnterprise.fakturaUsers()) delegateEmail

    let setMissingEFakturaDelegates delegateEmail = 
        [ for (id, email) in usersMissingDelegateFrom (VismaEnterprise.fakturaUsers()) delegateEmail do
            yield setDelegate id email delegateEmail ]
 
    let setMissingActiveDirectoryDelegates delegateEmail = 
        [ for (id, email) in usersMissingDelegateFrom (adUsers()) delegateEmail do
            yield setDelegate id email delegateEmail ]

    let checkIndividualUser usersDisplayName =
        let user = 
            VismaEnterprise.users () 
            |> Seq.filter(fun u -> u.DisplayName = usersDisplayName) 
            |> Seq.map( fun u -> (u.VismaId, u.Email)) 
        let missing = usersMissingDelegateFrom user delegateEmail
        missing

    let updateAllActiveDirectoryUsers () =
        let runRes = setDelegatesFor (adUsers()) delegateEmail
        runRes



let doActions () =
    failwith "This function is for interactive evaluation and should not be run."
    Integration.showActiveDirectoryUsersMissingDelegate delegateEmail
    Integration.setMissingActiveDirectoryDelegates delegateEmail |> Seq.filter (fun r -> match r with | Success x -> false | Error y -> true) |> Seq.toList |> ignore
    Integration.setDelegate 0 "Elizabeth.Gjessing@hfk.no" "vismapost@hfk.no"


let checkUser userMail =

    let testMail = userMail
    let yy = Exchange.showDelegates (0, testMail) 

    let ff = Exchange.setDelegate delegateEmail testMail
    Exchange.hasDelegate testMail delegateEmail



// Powershell manipulation...
// Using display names for identity causes duplicate errors
// This script requires a sesson configured against Exchange with credentials set: https://technet.microsoft.com/en-us/library/dd335083(v=exchg.160).aspx
let createPowershellScript () =
    let userList = ActiveDirectory.users () |> Seq.toList

    let template : Printf.StringFormat<string -> string -> string> = 
        """Add-ADPermission -Identity "%s" -User "%s" -AccessRights ExtendedRight -ExtendedRights "Send As" """


    let printUsers = userList |> Seq.filter(fun u -> u.Email |> exists) |> Seq.toList

    let scriptLines =
        let mutable i = 0
        [ for u in printUsers do
            yield sprintf """echo "%04i %s" """ i u.DisplayName
            i <- i + 1
            yield sprintf template u.DistinguishedName "Vismapost" ]

    for i in 10 .. 11 do
        printfn "%A" scriptLines.[i]

    System.IO.File.WriteAllLines("C:\\temp\set_sendas.ps1", scriptLines, System.Text.Encoding.UTF8)


