module MessageTypes


// server message types
type ServerMsg = 
    | Login
    | Logout
    | PostTweet of string
    | SubscribeTo of string
    | RegisterUser of string
    | ReTweet of string // may want to change to id
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