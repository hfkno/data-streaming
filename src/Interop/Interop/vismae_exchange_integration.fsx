(**
Visma Enterprise -> Exchange Integration
========================================


Integration overview
-------------------

  * Retreive "WebMail" users from Visma Enterprise 
  * Set "SendOnBehalf" in Exhcnage for those users


Integration details
-------------------

Visma Enterprise is located as "http://hfk-app01:8080/enterprise/enterprise?0"

Exchange offers C# wrappers, but they are heavyweight and uninteresting.  Powershell user management has been used instead.
*)



#load "ad_vismae_integration.fsx"
open Ad_vismae_integration

let webUsers = 
    [ { VismaEnterprise.User.Default with DisplayName = "hi1" }
      { VismaEnterprise.User.Default with DisplayName = "hi2" }
      { VismaEnterprise.User.Default with DisplayName = "hi3" }
      { VismaEnterprise.User.Default with DisplayName = "hi4" }
     ]










