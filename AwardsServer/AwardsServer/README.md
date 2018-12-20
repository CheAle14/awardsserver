## Awards Server

Central component to the [Awards Program](https://github.com/TheGrandCoding/awardsprogram)

### How does this work
You will need to run the AwardsServer.exe prior to any clients voting.  
Then, each [Client](https://github.com/TheGrandCoding/awardsprogram) should be run, with the correct IP having been corrected.


### Configuration

| Variable Name                   | Short Description                                                                                                            | Default Value                         | Notes                                                                                |
|---------------------------------|------------------------------------------------------------------------------------------------------------------------------|---------------------------------------|--------------------------------------------------------------------------------------|
| Maximum_Query_Response          | Maximum number of students given in response to a client's QUERY                                                             | 10                                    | N/A                                                                                  |
| Simultaneous_Session_Allowed    | Can the same student be connected twice                                                                                      | False                                 | If True, will randomly add 3 numbers to name                                         |
| Allow_Modifications_When_Voting | Allow GUI's tables to be double-click-edited even after a person connects                                                    | False                                 | Default is false, because editing account names etc while connected may cause issues |
| Maximum_Concurrent_Connections  | This many people can be voting at once. After this amount, a queue begins.                                                   | 15                                    | This can be edited once started, will update dependant on the Heartbeat below.       |
| Time_Between_Heartbeat          | Seconds between each connection is sent a message to confirm it is connected                                                 | 5                                     | Will also refresh the queue at this interval                                         |
| Display_Recieve_Client          | Display messages that are sent by a client                                                                                   | true                                  | If "Only_Show_Above_Severity" is anything above Debug, this Option is useless        |
| Display_Send_Client             | Display messages sent by the server to clients                                                                               | true                                  | If "Only_Show_Above_Severity" is anything above Debug, this Option is useless        |
| Only_Show_Above_Severity        | Only display messages in the console that are above the selected severity. Note that messages are still logged to text file. | Debug                                 | Order, from lowest -> highest: Debug Info Warning Error Severe                       |
| ServerIP_File_Path              | Location of the "ServerIP" file, which contains the Server's local IP address for github update-use                          | "..\..\..\ServerIP" Three folders up. | [This file is on github](https://raw.githubusercontent.com/TheGrandCoding/awardsserver/master/AwardsServer/ServerIP), and is used by the clients to get the latest IP              |
|                                 |                                                                                                                              |                                       |                                                                                      |
|                                 |                                                                                                                              |                                       |                                                                                      |
|                                 |                                                                                                                              |                                       |                                                                                      |
|                                 |                                                                                                                              |                                       |                                                                                      |
|                                 |                                                                                                                              |                                       |                                                                                      |


- - -

### Further things
#### Updating a Student's information
Head on over to the "[Students][Students]" tab of the Server's UI.  
Assuming that no person has connected yet, you should be able to double click the entry you want to change and modify it, then click off or hit enter to save.  
If the above is not true, then you will need to make sure the "Allow student data to be modified.." option is 'ticked' or set to True in the "[Options][Options]" window.  

#### Updating or Adding a Category
Head over to the [Categories][] tab.  
You should be able to double click and edit the prompt of the category to edit and change it  
If you want to add a category, then you can use the far bottom-right empty cell and double click that.  
**Note: you cannot edit the ID of a category.**  

#### Removing a Student's vote
On the [Students][] tab, you can uncheck the 'Voted' tickbox, then hit enter, and confirm that is what you want to do.  

#### View the Winners
On the [Winners][] tab, you can see, for each category, the:  
- Male winner  
- Female winner  
- For both, the number of votes given to that person, in the bracket.  

If there are multiple winners, they are all displayed.

- - -

## Common Errors

A list of some errors that may occur, what causes them, and how to fix them.

### User not found
**Full text:** `UnknUser: System.ArgumentException: User not found: '<name>'`  
**Cause:** the client's accountname is not present in the database  
**Solution:** add the username to the database, re-run the server.  
**Issue:** this cannot be fixed without restarting the entire server. (as of now: to be changed)  

### No students have been loaded.
**Full text:**   
`App: No students have been loaded. Assuming that this is an error.`  
`App: This error will continue to occur until atleast one student is added to the 'Database.accdb' file`  
**Cause:** there are zero students inside the Database file.  
**Solution:** add students to the Database.accdb file that is within the same folder as the Server's .exe  
**Issue:** again, requires a server restart - though no one could be voting anyway.  

### User <name> changed
**Full text:** `User '<name1>' changed, discarding vote for '<name2>' in category <category>`  
**Cause:** the `<name1>` is no longer in the Database (ie: it has been changed), so the vote by that person for `<name2>` cannot be accepted.  
**Solution:** either fix the name back to what it should be, or remove/transfer the votes to the correct username.  
**Issue:** again, requires server restart.  
**Related:** this will also occur if `<name2>` is modified.  

### The "Microsoft.ACE.OLEDB.12.0" provider is not registered
**Full text:** `System.InvalidOperationException: The 'Microsoft.ACE.OLEDB.12.0' provider is not registered on the local machine`  
**Cause:** You don't have the required Microsoft component packages that support the version of the database provider that we use.  
**Solution:** Download the needed packages. I won't directly link to any, since I dont want to be responsile for any viruses  


### Remove the "-" from <name> 's name
**Full text:** `Remove the '-' from <accountName>: <FirstName> <LastName> (<Tutor>) 's name`  
**Cause:** Names cannot contain dashes (the - charactor, given that )

- - -

## Common Warnings

### User <name> has invalid account name
**Full text:** `User '<first_name> <last_name> <tutor> <sex> <account_name> has invalid account name'`  
**Cause:** Account names are expected to have the same number of charactors as 'cheale14'  
**Solution:** Check that the account name given is correct. If it is, the warning can be ignored.  
**Is Critical?** Not particularly.  

Other errors may be added

- - -

## Console Commands

### remove_all_votes
**Syntax:** `remove_all_votes`  
**Paramaters:** none  
**Action:** removes all votes by students, with confirmation  

### copy_winners
**Syntax:** `copy_winners`  
**Paramaters:** none  
**Action:** prints the winners in a format of `[PROMPT]: [Male Winners] -- [Female Winners]` to both the log text/console and a HTML file  




[Students]: https://github.com/TheGrandCoding/awardsserver/blob/master/UI_TABS.md#students
[Categories]: https://github.com/TheGrandCoding/awardsserver/blob/master/UI_TABS.md#categories
[Winners]: https://github.com/TheGrandCoding/awardsserver/blob/master/UI_TABS.md#winners
[Options]: https://github.com/TheGrandCoding/awardsserver/blob/master/UI_TABS.md#server-options
[CurrentQueue]: https://github.com/TheGrandCoding/awardsserver/blob/master/UI_TABS.md#current-queue