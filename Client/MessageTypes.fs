module MessageTypes

// Message type deffinitions for serialization
type UsernameMsg(username: string) =
  member this.username = username
type TweetMsg(tweet: string, user: string) = 
  member this.tweet = tweet
  member this.user = user
type ReTweetMsg(origId: int, user: string) = 
  member this.origId = origId
  member this.user = user
type SimInitSubsMsg(username: string, numSubs: int) =
  member this.username = username
  member this.numSubs = numSubs


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
    | StartSimulation of string
    | RecieveStatistics of int * float


// user message types
type MainMsg = 
    | QueryTweets of (int * string * string)[]
