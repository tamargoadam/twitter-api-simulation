module MessageTypes


// server message types
type ServerMsg = 
    | Login of string
    | Logout of string
    | PostTweet of string * string
    | SubscribeTo of string * string
    | RegisterUser of string
    | ReTweet of string * string * int
    | GetTweetsSubscribedTo of string
    | GetTweetsByMention of string
    | GetTweetsByHashtag of string


// user message types
type UserMsg = 
    | ReceiveTweet of string
    | ReceiveTweets of string[]


// client supervisor message types
type ClientMsg =
    | StartSimulation
    | RecieveStatistics of int * int * int // change to whatever types neccessary for decided upon statistics