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
open Microsoft.Exchange.WebServices.Data
open Ad_vismae_integration
open Configuration

let webUsers = 
    [ { VismaEnterprise.User.Default with DisplayName = "hi1" }
      { VismaEnterprise.User.Default with DisplayName = "hi2" }
      { VismaEnterprise.User.Default with DisplayName = "hi3" }
      { VismaEnterprise.User.Default with DisplayName = "hi4" }
     ]


let uname, pass, domain, delegateEmail
    = Configuration.ExchangeAdmin.UserName, 
      Configuration.ExchangeAdmin.Password, 
      Configuration.ExchangeAdmin.Domain, 
      "vismapost@hfk.no"

let service = new ExchangeService(
                    Url = new Uri("https://mail.hfk.no/EWS/Exchange.asmx"), 
                    Credentials = new WebCredentials(uname, pass, domain))

service.ImpersonatedUserId <- new ImpersonatedUserId(ConnectingIdType.SmtpAddress, "aarcomy@hfk.no")
let userMailbox = new Mailbox("aarcomy@hfk.no") // "anette.ovreas@hfk.no")

let delegates = service.GetDelegates(userMailbox, true)
for d in delegates.DelegateUserResponses do
    printfn "%s" d.DelegateUser.UserId.DisplayName


let setDelegate (service : ExchangeService) (delegateEmail : string) (forUser : string) = 
    let delegateUser = new DelegateUser(delegateEmail)
    let scope  = System.Nullable(MeetingRequestsDeliveryScope.DelegatesAndMe)
    service.ImpersonatedUserId <- new ImpersonatedUserId(ConnectingIdType.SmtpAddress, forUser)
    let userMailbox = new Mailbox(forUser)
    service.AddDelegates(userMailbox, scope, delegateUser)



