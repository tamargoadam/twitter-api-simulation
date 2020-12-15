# Client

### Client simulator functionality:
- Simulates requested number of users
- Each user has a number of subscribers randomly sampled from a Zipf distribution
- Users tweet proportional to the number of subscribers they have (~numSubscribers/10)
- Users tweets have hashtags 11% of the time, have mentions 49% of the time, and retweet tweets they see 13% of the time
  - Usage statistics found [here](https://www.aaai.org/ocs/index.php/ICWSM/ICWSM11/paper/view/2856/3250)

### Command to run:
`dotnet run <num_users>` 
