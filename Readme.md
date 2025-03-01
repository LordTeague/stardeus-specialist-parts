This is a mod designed to provide body parts that buff stats but have limited abilities--hence the specialization. 

Unfortunately, there are three intended features that I haven't been able to implement: 
1. (Confirmed not yet possible) Having an initial cooldown for the continuous attacks of the drill and saw hands. 
  a. For the time being, a .5 second startup and .1 second cooldown has been set here, as it'll be a long while before the related feature suggestion I made here is added. 
2. (WIP) Enabling the Demolition skill without also enabling the Build skill, and likewise for not enabling the other skills related to Plants. While the other files are set up for this, this apparently requires a code modification at present. 
  a. I have no idea how to properly override this in C#, as I'm primarily used to coding in Java. 
  b. My intent is to allow multiple single abilities be the final one required to enable a given task. 
3. (Not Started) Actually allowing stat boosts from these body parts. Desired implementation is having them boost job efficiency, but not affect learn rate (which is still based off the mind system). 

Also, I have unfortunately been unable to get a proper 4.8.1 NDP compilation of this code, and at this point I'm out of ideas to fix it. See the stardeus steam forum topic for details on where I'm stuck there. 