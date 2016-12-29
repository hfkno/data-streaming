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


#r "../../packages/Microsoft.Exchange.WebServices/lib/40/Microsoft.Exchange.WebServices.dll"
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

    let getDelegatesFor userEmail = service.GetDelegates(impersonatedMailbox userEmail, true).DelegateUserResponses

    let showDelegates (vismaId, userEmail) = 
        printfn "showing user : %i - %s"vismaId userEmail
        for d in getDelegatesFor userEmail do
            match d.DelegateUser with
            | null -> printfn "Got a null user back from a delegate of user %i:%s" vismaId userEmail
            | _    -> printfn "%s" d.DelegateUser.UserId.DisplayName


    let hasDelegate (vismaId, userEmail) (delegateEmail:string) =
        (getDelegatesFor userEmail)
            .Any(fun d -> 
                match d.DelegateUser with
                | null -> false
                | _ -> d.DelegateUser.UserId.PrimarySmtpAddress.ToLower() = delegateEmail.ToLower())

    let setDelegate (delegateEmail : string) (forUser : string) = 
        let delegateUser = new DelegateUser(delegateEmail)
        let scope  = System.Nullable(MeetingRequestsDeliveryScope.DelegatesAndMe)
        service.AddDelegates(impersonatedMailbox forUser, scope, delegateUser)



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
        } 
        |> Seq.toList



module Integration =

    let usersMissingDelegates delegateEmail =
        for (i,e) in  VismaEnterprise.fakturaUsers() do
            if not <| Exchange.hasDelegate (i, e) delegateEmail then
                printfn "Missing delegate - user %i:%s lacks email delegation to %s" i e delegateEmail


    let setAllDelegates () =

        for (id, email) in VismaEnterprise.fakturaUsers() do 
            printfn "Setting delegate for %i:%s" id email
            Exchange.setDelegate delegateEmail email |> ignore



//Integration.usersMissingDelegates delegateEmail
//Integration.setAllDelegates()
