﻿(**
Visma Enterprise -> Exchange Integration
========================================


Integration overview
-------------------

  * Retreive "WebMail" users from Visma Enterprise 
  * Set "SendOnBehalf" in Exhcnage for those users


Integration details
-------------------

Visma Enterprise is located as "http://hfk-app01:8080/enterprise/enterprise?0"

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
#load "ad_vismae_integration.fsx"

open System
open Microsoft.Exchange.WebServices.Data
open Ad_vismae_integration

let webUsers = 
    [ { VismaEnterprise.User.Default with DisplayName = "hi1" }
      { VismaEnterprise.User.Default with DisplayName = "hi2" }
      { VismaEnterprise.User.Default with DisplayName = "hi3" }
      { VismaEnterprise.User.Default with DisplayName = "hi4" }
     ]


//PowerShell.Create().AddScript()

let email, pass, domain = "", "", "mail.hfk.no"
let service = new ExchangeService()
service.Credentials <- new WebCredentials(email, pass, domain)
service.Url <- new Uri("https://mail.hfk.no/EWS/Exchange.asmx")





//service.Url <- 
//let url = service.AutodiscoverUrl(email, (fun (redirectionUrl:string) -> redirectionUrl.ToLower().StartsWith("https://") ))



