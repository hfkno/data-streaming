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


//PowerShell.Create().AddScript()

let email, pass, delegateEmail, domain 
    = Configuration.ExchangeAdmin.UserName, Configuration.ExchangeAdmin.Password, "vismapost@hfk.no", "mail.hfk.no"

printfn "%s %s %s" email delegateEmail domain




// Working code... Local account, only access to my own mailbox...
let es = new ExchangeService()
es.UseDefaultCredentials <- true
es.Url <- new Uri("https://mail.hfk.no/EWS/Exchange.asmx")
let emb = new Mailbox("aarcomy@hfk.no") // "anette.ovreas@hfk.no")
let eds = es.GetDelegates(emb, true)
let service = es

// Explicit connection, not working...
let service = new ExchangeService()
service.Credentials <- new WebCredentials(@"adm_aarcomy", "", "hfk")
service.ImpersonatedUserId <- new ImpersonatedUserId(ConnectingIdType.SmtpAddress, "aarcomy@hfk.no")


// use impersonation FROM aarcomy TO adm_aarcomy??
//service.ImpersonatedUserId <- new ImpersonatedUserId(ConnectingIdType.SmtpAddress, "aarcomy@hfk.no")

service.Url <- new Uri("https://mail.hfk.no/EWS/Exchange.asmx")
let userMailbox = new Mailbox("aarcomy@hfk.no") // "anette.ovreas@hfk.no")
let delegates = service.GetDelegates(userMailbox, true)
for d in delegates.DelegateUserResponses do//for d in delegates do
    printfn "%s" d.DelegateUser.UserId.DisplayName


let mailboxQuery = [| new MailboxQuery("Aaron", [|  |]) |]
let searchParameters = new SearchMailboxesParameters(SearchQueries = mailboxQuery)
let searchBox = service.SearchMailboxes(searchParameters) |> Seq.head
let userMailbox = new Mailbox("aarcomy@hfk.no")
let delegates = service.GetDelegates(userMailbox, true)
service.TraceEnablePrettyPrinting <- true
service.TraceEnabled <- true
service.TraceFlags = TraceFlags.All

Environment.UserDomainName
Environment.UserName


let scope  = System.Nullable(MeetingRequestsDeliveryScope.DelegatesAndMe )

let delegateUser = new DelegateUser(delegateEmail)

service.AddDelegates(userMailbox, scope, delegateUser)

//service.AddDelegates(usermailbox, MeetingRequestsDeliveryScope.DelegatesAndMe, [| delegateuser |])



//service.Url <- 
//let url = service.AutodiscoverUrl(email, (fun (redirectionUrl:string) -> redirectionUrl.ToLower().StartsWith("https://") ))



