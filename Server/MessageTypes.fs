module MessageTypes


// Message type deffinitions for serialization
type UsernameMsg = {username:string} 
type HashtagMsg = {tag:string} 
type TweetMsg = {tweet:string; user:string}
type ReTweetMsg = {origId:int; tweet:string; user:string}
type SubscribeMsg = {subTo:string; user:string}
type TweetData(id: int, tweet: string, user: string) = 
  member this.id = id
  member this.tweet = tweet
  member this.user = user
type TweetsMsg(tweets:TweetData[]) =
  member this.tweets = tweets
type SimInitSubsMsg = {username: string; numSubs: int}

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
    | ReceiveTweet of TweetData // id, tweet, user
    | RequestLogin of string


// client supervisor message types
type ClientMsg =
    | StartSimulation of Akka.Actor.IActorRef
    | RecieveStatistics of int * float


// user message types
type MainMsg = 
    | QueryTweets of TweetsMsg
