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
  
type ReceiveTweetMsg = {id: int; tweet: string; user: string}


// user message types
type UserMsg = 
    | ReceiveTweet of int * string * string // id, tweet, user


// client supervisor message types
type ClientMsg =
    | StartSimulation