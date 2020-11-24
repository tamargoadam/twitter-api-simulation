module User

open MessageTypes

open Akka.FSharp


let twitterUser (numSubscribers: int) (mailbox : Actor<UserMsg>) = 
    let rec loop () = 
        actor {
            let! msg = mailbox.Receive()
            let sender = mailbox.Sender()
            // run functions to record measurments that can be sent back to the client supervisor
            match msg with
                | ReceiveTweet tweet -> ignore
                | ReceiveTweets tweets -> ignore
            return! loop()
        }
    loop()