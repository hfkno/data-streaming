



type MessagagingSystem =
    | Message
    | Channel
    | Sender
    | Receiver


type Message = 
    { Header : string
      Body : string }


type MessageTransmission =
    | Create
    | Send
    | Deliver
    | Receive 
    | Process
