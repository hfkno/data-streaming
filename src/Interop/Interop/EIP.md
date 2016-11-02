


integrated applications are independent programs that can each run by itself, yet that function by coordinating 
with each other in a loosely coupled way




Fundamental challenges:

	o Networks are unreliable
	o Networks are slow
	o Any two applications are different
	o Change is inevitable



Common solutions:

	o File transfer
	o Shared Database
	o Remote Procedure Invocation
	o Messaging



Messaging is a technology that enables high-speed, asynchronous, program-to-program communication with reliable delivery.


A channel behaves like a collection or array of messages, but one that is "magically" shared across multiple 
computers and can be used concurrently by multiple applications


A message contains two parts, a header and a body. 
The header contains meta-information about the message
The body contains the data being transmitted and is ignored by the messaging system


The main task of a messaging system is to move messages from the sender’s computer to 
the receiver’s computer in a reliable fashion



Message Transmission:

	1. Create — The sender creates the message and populates it with data.
	2. Send — The sender adds the message to a channel.
	3. Deliver — The messaging system moves the message from the sender’s computer to the receiver’s computer.
	4. Receive — The receiver reads the message from the channel.
	5. Process — The receiver extracts the data from the message. 




Messaging is more immediate than File Transfer, better encapsulated than Shared Database, and more reliable than
Remote Procedure Invocation




A messaging system can be a universal translator between the applications that works with each 
one’s language and platform on its own terms, yet allows them to all communicate through a common messaging
paradigm


Messaging benefits:

	Remote Communication
	Platform/Language Integration
	Asynchronous Communication
	Variable Timing
	Throttling
	Reliable Communication
	Disconnected Operation
	Mediation
	Thread Management



Messaging challenges:

	Complex programming model
	Sequence issues
	Synchronous scenarios
	Performance
	Limited platform support
	Vendor lock-in




Independent applications that can each run by itself, but coordinate with each other in a loosely coupled way



Types of integration projects:

	Information Portals
	Data Replication
	Shared Business Functions
	Service-Oriented Architectures
	Distributed Business Processes
	Business-to-Business Integration




Objects that interact in a distributed system need to be dealt with in ways that are intrinsically 
different from objects that interact in a single address space


The core principle behind loose coupling is to reduce the assumptions two parties make 
about each other when they exchange information

A common data format, queuing channels, and transformers help turn a tightly coupled solution into a loosely coupled solution


Managing coupling:


	Location:

	A channel is a logical address that both sender and receiver can agree
	on the same channel without being aware of each other’s identity.
	Using channels resolves the
	location-dependency, but still requires all components to be available at the same time if the
	channel is implemented using a connection-oriented protocol


	Temporal:

	In order to remove this temporal
	dependency we can enhance the channel to queue up sent requests until the network and the
	receiving system are ready. To support queuing of requests inside the channel, we need wrap
	data into self-contained messages so that the channel knows how much data to buffer and deliver
	at any one time. 


	Structural:

	Lastly, the two systems still depend on a common data format. We can remove
	this dependency by allowing for data format transformations inside the channel




Loosley coupled integrations:

Regardless of the payload, this piece of data needs to be understood by both ends and needs to be transported. 

We need a communications channel that can move information from one application to the other.

In the channel we place a message: a snippet of data that has an agreed-upon meaning to both applications.







Cases of semantic dissonance are much harder to deal with than inconsistent data formats







