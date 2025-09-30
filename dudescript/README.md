```
send
	#prompt is optional, this is used when the prompt changes with a command.
	#print is optional and can be "print" or "print_nbl". "print_nbl" = do not print any blank lines
	#cmd_timeout is optional

	,command_to_send,/prompt/,print,cmd_timeout
	,command_to_send,,print,cmd_timeout
	,command_to_send

grab
	#used to populate a variable with results of a previous send command. the regular express is match against the results of the previous send.

	#variable is populated with whatever is within parenthesis /hello (\w+?)$/ would put anything that matches \w+? after hello in the variable. if the regex has multiple matches then you can specify which match you want with the :match_number example /hello (\w+?)$/:2 would populate the variable with the 2nd match.

	#from variable is optional. useful when grabbing something the response of a do command.

	,variable,/(regex)/:match_number,from_variable


check

	#valid check_operation if a variable is supplied "re,!re,eq,gt,gte,lt,lte,ne"
	#valid check_operation without variable supplied "re,!re"

	#string can be in string format or regular expressions with slash starting and end.
	#true anchor is an anchor to jump to if preceding statement is true. if ommited then we just continue to next line on true
	#false_anchor is an anchor to jump to if preceding statement is false

	#value of variable is passed to true/false anchor and can be called using the anchor ex. <!#true_anchor#!>

	,variable,check_operation,"check_string",true_anchor,false_anchor
	,variable,check_operation,/check_string_regex/,true_anchor,false_anchor
	,variable,check_operation,"check_string",,false_anchor
	,,check_operation,/check_string_regex,,false_anchor
wait
	#sleeps for x seconds
	,wait_in_seconds

do
	#runs a local system command

	#variable_for_output_of_command is optional
	#set value in chomp to remove newline from cmd response
	#print is optional

	,local command,variable_for_output_of_command,print,chomp
	,local command,variable_for_output_of_command
	,local command


print
	#prints a line to screen with newline (use do,echo -n message) if you do not want a newline	 
	,message

printf
	#prints a line with printf to screen. format followed by variables. No newline is included with this. use \n in the format 	 
	,format,var,var

write
	#writes something to a file.

	#supply filename with > to overwrite a file or a >> to append to a file in from of the filename ex: ">./foo"

	#some operating systems will echo the command being run in the output. if you pass any value in the skip_first_element section then the first element of the response aka the command will not be written to the file.

	#if you supply anything in print_nbl then it will not print any blank lines if any exist from the last command

	#if a variable is supplied then that will be what is written to the file. if no variable is supplied then the output from the last command executed will be written to the file. or you write a specific string "hello"

	,>filename,variable,print_nbl,skip_first_element
	,>>filename,variable,print_nbl,skip_first_element
	,>filename,variable,print_nbl
	,>filename,variable,
	,>filename,variable,
	,>filename,,,skip
	,>>filename,"line to print"

writef
	#writes something to a file with a custom format

	#supply filename with > to overwrite a file or a >> to append to a file in from of the filename ex: ">./foo"

	#some operating systems will echo the command being run in the output. if you pass any value in the skip_first_element section then the first element of the response aka the command will not be written to the file.

	#if you supply anything in print_nbl then it will not print any blank lines if any exist from the last command

	#if a variable is supplied then that will be what is written to the file. if no variable is supplied then the output from the last command executed will be written to the file. or you write a specific string "hello"

	,>filename,format,delimiter,print_nbl,skip_first_element,<!#variable1#!>,<!#variable2#!>
	,>>filename,format,,print_nbl,skip_first_element,<!#variable1#!>,<!#variable2#!>
	,>filename,format,,print_nbl,,<!#variable1#!>,<!#variable2#!>
	,>filename,format,,,,<!#variable1#!>,<!#variable2#!>,<!#variable2#!>
	,>filename,format,,,skip,<!#variable1#!>,<!#variable2#!>

	## example
	# writef,>./test.txt,%s\t%s\t%s\t%s\n,,,,<!#i#!>,<!#j#!>,<!#k#!>,not quoted,"quoted"
	# writef,>>./show_config_aaa,%s\t%s\t%s\n, ,,,<!#checkaaa:<!#i#!>#!>

set
	#sets/changes or increments a variable
	#find can be blank to set or change a variable

	,variable,/find/,/replace/
	,variable,,"value"
	,variable,,value
	,variable,,+x
	,variable,,-x

goto
	#jumps to an anchor
	,anchor

include
	#includes a file from include directory only

	#functions very similar to goto, include file is added as a function so you must have a return in your include file
	#variables can be passed to includes

	,filename
	,filename,variable,variables

return
	#if you used goto or check to move to an anchor then return will take you back to where you jumped from so you can continue. similar to ending a function.


interactive
	# drops to a psuedo interactive shell. quit by typing "q!"

	# this could effect the continuation of the conversation if the next command is expecting the last_reponse or be something and you changed it by running a command interactivly.

	# if the prompt changes due to a command you can change it along with the command (comma seperated)

		#example using login method on cisco
								   login,Username:
								   chris,Password:
							       mypassword,# ?$


	,noblanklines


exit
	#exit program

	#you can specify a exit message

	,message

::anchor_name::
	<!#anchor_name#!> will be populated with the variable used in the check function that moved you to this anchor.

	# anchor take used for check or goto to move around.


#comment

	#add a comment in the file.




CSV Files supported predefined fields
	"host"
	"dudescript"
	"port"
	"delay"
	"timeout"
	"transport"
	"username"
	"password"
	"personality"





######## Stuff to add/change/fix/break more ###########

fix stuff'n'things


```
