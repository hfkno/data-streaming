﻿

## Plan

  - Tjenestealbum
  - Helhetlig integrasjonplattform
  - Integration platform Demo
  - Sharing & Learning setup

  - Identity establishment
  - Overvaaking
  - BizTalk



### Study

  o BizTalk
  	o BPEL
  o EIP
  o Business process modeling



### Holistic integration platform

  o Basic message transfer
  	o Web -> Akka.Net -> RabbitMq -> Kafka
  o Show message status & movement
  o Share available schemas (input & output)
  o Show current status
  	o Nodes & messages
  o Demo
  	o From website into files, KAFKA, other messages, etc... and back again via sockets?



### Tjenestekatalog

   o List services
   o Show service information
   o Service SLA information
   o Retrieve SLA information programmatically
   o List aviailable schemas...
   o Service discovery [lighthouse? via akka.net cluster?]




#### Design

	o Primary portal enabled with lighthouse and swagger
	o declarative overview of services
	o Links to sub-swagger instances and swagger.json files from "owned" services
	o Links to generated swagger files for third party services



### BizTalk


  o Basic education
  o Testing
  o Simple orchestrations
