module MessageTypes


// server message types
type ServerMsg = 
    | Login of string
    | Logout of string
    | PostTweet of string * string
    | SubscribeTo of string * string
    | RegisterUser of string
    | ReTweet of int * string * string
    | GetTweetsSubscribedTo of string
    | GetTweetsByMention of string
    | GetTweetsByHashtag of string
    | SimulateSetInitialSubs of string * int


// user message types
type UserMsg = 
    | ReceiveTweet of int * string * string // id, tweet, user
    | ReceiveTweets of (int * string * string)[]


// client supervisor message types
type ClientMsg =
    | StartSimulation of Akka.Actor.IActorRef
    | RecieveStatistics of int * int * int // change to whatever types neccessary for decided upon statistics