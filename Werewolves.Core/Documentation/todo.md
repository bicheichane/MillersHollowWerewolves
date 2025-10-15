todo:

- add helper functions to validate expected input types
- add helper function to search game history log by log class type, optional number of turns ago (0 for current turn, 1 for 1 turn ago, etc.), optional GamePhase, optional lambda for log subclass specific additional filtering
- in general, look to extract duplicate boilerplate code out to smaller functions


ProcessNightAction needs to return a HandlerResult instead and let the game service know if it should advance or not


implementation prompts:

plan out the required changes to implement **phase 2 step 1 (seer)** in `implementation-roadmap.md` . do not start implementing it yet. seriously and thoughtfully consider the currently defined architecture in `architecture.md` , consider if there are any changes you think would make sense to make for the implementation effort in question. if you find any, run them by me and state your case first, so I can assess whether or not I want to go forward with them. 


proceed with the implementation along the lines of what you're previously described. only go one file at a time, and ask me to confirm if I approve of your implementation of that specific file before continuing to the next one. be prepared to refactor as you go