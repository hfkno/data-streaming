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

Exchange offers C# wrappers, but they are heavyweight and uninteresting.  Powershell user management against exchange has been used instead...

Details on connecting to Exchange via Powershel: https://technet.microsoft.com/en-us/library/dd335083(v=exchg.160).aspx




Setting up the Exchange Shell on local Windows Machine 
------------------------------------------------------
From : https://technet.microsoft.com/en-us/library/dd335083(v=exchg.160).aspx

    Set-ExecutionPolicy RemoteSigned
    $UserCredential = Get-Credential

    mail.hfk.no
*)



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



let service = new ExchangeService(
                    Url = new Uri("https://mail.hfk.no/EWS/Exchange.asmx"), 
                    Credentials = new WebCredentials("adm_aarcomy", "", "hfk"))
service.ImpersonatedUserId <- new ImpersonatedUserId(ConnectingIdType.SmtpAddress, "aarcomy@hfk.no")
let userMailbox = new Mailbox("aarcomy@hfk.no") // "anette.ovreas@hfk.no")
let delegates = service.GetDelegates(userMailbox, true)
for d in delegates.DelegateUserResponses do//for d in delegates do
    printfn "%s" d.DelegateUser.UserId.DisplayName



let service = new ExchangeService(
                    Url = new Uri("https://mail.hfk.no/EWS/Exchange.asmx"), 
                    Credentials = new WebCredentials("adm_aarcomy", "", "hfk"))





let fakturaUsers () =
    let allUsers = VismaEnterprise.UserService.users()
    let allGroups = VismaEnterprise.UserService.groups() |> Seq.toList

    let fakturaUserIds = set [ for g in allGroups do
                                   if fakturaGroups.Contains(g.Name) then
                                       for m in g.Members do yield m ]

    query {
        for u in allUsers do
        where (fakturaUserIds.Contains(u.VismaId))
        select u.Email
    } 
    |> Seq.toList


let fu = fakturaUsers ()


let showDelegates userEmail = 

    service.ImpersonatedUserId <- new ImpersonatedUserId(ConnectingIdType.SmtpAddress, userEmail)
    let userMailbox = new Mailbox(userEmail)

    let delegates = service.GetDelegates(userMailbox, true)
    for d in delegates.DelegateUserResponses do
        printfn "%s" d.DelegateUser.UserId.DisplayName

showDelegates "aarcomy@hfk.no"



for u in fu do showDelegates u


let setDelegate (service : ExchangeService) (delegateEmail : string) (forUser : string) = 
    let delegateUser = new DelegateUser(delegateEmail)
    let scope  = System.Nullable(MeetingRequestsDeliveryScope.DelegatesAndMe)
    service.ImpersonatedUserId <- new ImpersonatedUserId(ConnectingIdType.SmtpAddress, forUser)
    let userMailbox = new Mailbox(forUser)
    service.AddDelegates(userMailbox, scope, delegateUser)



