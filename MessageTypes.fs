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
    | SimulateSetExpectedTweets of int

// user message types
type UserMsg = 
    | ReceiveTweet of int * string * string // id, tweet, user


// client supervisor message types
type ClientMsg =
    | StartSimulation of Akka.Actor.IActorRef
    | RecieveStatistics of float


// user message types
type MainMsg = 
    | QueryTweets of (int * string * string)[]