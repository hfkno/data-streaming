
(**
# Active Directoy -> Visma Enterprise Integration

## Integration overview

  * Retreive users from Visma Enterprise
  * Retreive users from Active Directory
  * Synch changes from active directory with Visma user data
  * Push user changes to Visma Enterprise through their user management webservice
*)


(** 
## Integration details
Initial connection to LDAP for user retrieval
*)

(*** include: ldap-connect ***)


(*** define: ldap-connect ***)
let helloWorld() = printfn "Hello world!"

