#!/usr/bin/perl -w
#############################################################################
# Proof of concept code
#
# project: dudescript
#
# Created 01/10/2010
# Version .06
#
# Made by: Chris Dudek (chris@nwcd.net)
#
#
# Description: the idea behind this program was to be able to
#              script multiple fairly complex conversations with
#              any device over ssh/telnet. send commands, analyze
#              responses. basicly be able to perform changes and
#              verify those changes.
#
#
# Notes: 01/10/2010 : So I consider this proof of concept code because I was simply testing
#        if this concept could be done at all and useful. I started
#        with no requirements and only the ability to perform send, grab,
#        check. Because I didnt have a full picture of everything I needed
#        a lot of features such as the ghetto method to handle loops was
#        kinda hacked in lol. Plus im not a developer but i did stay at a
#        holiday inn last night!
#
# Notes 2: 05/10/2012 : While I love what Net::Appliance::Session allows me to do I
#          hate the number of dependencies it has. :)
#
# Notes 3: 5/8/2015 : wow looking at your old code is just ugly.. hah.. im
#                     still not a good coder but damn better than i was!
#
#############################################################################

use strict;
use warnings;
use Getopt::Std;
use Data::Dumper;
use File::Basename;
use List::Util qw(first);
use Scalar::Util qw(looks_like_number);
use Net::Appliance::Session;

$|=1;
#retain name for use in usage function
my $program_name = $0;

#hide password is supplied on command line
($0 = "$0 @ARGV") =~ s/-p\s*?.+?\b/-p xxxxxxx /;

my $s;
my $cur_loop = 0;
my $num_loops;
my %opts;
my %variables;
getopts('NCcv:tnDhV:w:u:p:T:H:P:b:f:F:d:', \%opts);

# exit with usage
if ($opts{h}){usage();}

# exit and display usage unless we have required perameters
if (!$opts{f} && !$opts{F} ){usage();};
if ($opts{F} && !$opts{f}){
        #just sets a default personality because this can be set in the list specified with -F
        $opts{f} = "cisco";
}

my $check_list_vars = $opts{c};
my $check_chat_vars = $opts{C};
my $username = $opts{u};
my $password = $opts{p};
my $host = $opts{H};
my $do_login = ($opts{n}) ? 0 : 1;
my $filename = $opts{f};
my $csvfilename = (defined $opts{F}) ? $opts{F} : "";
my $variables = $opts{V};
my $debug = (defined $opts{D}) ? $opts{D} : 0;
my $no_connect = (defined $opts{N}) ? $opts{N} : 0;
my $delay = (defined $opts{d}) ? $opts{d} : 0;
my $verbose = (defined $opts{v}) ? $opts{v} : 0;
if(exists $opts{v} && $verbose == 0){$verbose = 1;}
my $transport = (defined $opts{t}) ? 'Telnet' : 'SSH';
my $timeout = (defined $opts{T}) ? $opts{T} : 30;
my $personality = (defined $opts{w}) ? $opts{w} : basename(dirname($filename));
my $port;
if ($transport eq "SSH"){
   $port = (defined $opts{P}) ? $opts{P} : 22;
}else{
   $port = (defined $opts{P}) ? $opts{P} : 23;
}

if ($check_list_vars){
        my ($file_contents) = read_conversation_file();
        my ($required_variables_ref) = process_conversation_file($file_contents);
        print_required_params($required_variables_ref);
        exit 0;
}

#move variables passed from command line into hash for replacment in conversation
if($variables){
        chomp( (%variables) = map { split "," } my (@a) = $variables );
}

my ($csv_ref,@csv_vars);
if ($csvfilename){
        $csv_ref = read_list_file();
        $num_loops = scalar(@$csv_ref);
}else{
        $num_loops = 0;
}

#loop for csvfile support
do
{

#find line to redefine veriables (starts with >)
if(@$csv_ref[$cur_loop] && @$csv_ref[$cur_loop] =~ m/^>/){
        #found redefine definition line.
        @$csv_ref[$cur_loop] =~ s/^>//;

        my @def_line = split("," , @$csv_ref[$cur_loop]);
        undef (@csv_vars);
        foreach my $key (@def_line){
                push(@csv_vars, $key);
        }

        $cur_loop++;
}


if ($num_loops > 0){
        my @cur_vars = split("," , @$csv_ref[$cur_loop]);
        @cur_vars = map { s/#COMMA#/,/g; $_ } @cur_vars;
        for my $i (0 .. scalar(@csv_vars)){
                if ($csv_vars[$i] eq "host"){
                        $host = $cur_vars[$i];
                }elsif($csv_vars[$i] eq "filename"){
                        $filename = $cur_vars[$i];
                }elsif($csv_vars[$i] eq "port"){
                        $port = $cur_vars[$i];
                }elsif($csv_vars[$i] eq "delay"){
                        $delay = $cur_vars[$i];
                }elsif($csv_vars[$i] eq "timeout"){
                        $timeout = $cur_vars[$i];
                }elsif($csv_vars[$i] eq "transport"){
                        $transport = $cur_vars[$i];
                }elsif($csv_vars[$i] eq "username"){
                        $username = $cur_vars[$i];
                }elsif($csv_vars[$i] eq "password"){
                        $password = $cur_vars[$i];
                }elsif($csv_vars[$i] eq "personality"){
                        $personality = $cur_vars[$i];
                }else{
                        $variables{$csv_vars[$i]} = $cur_vars[$i];
                }
        }

}




#push host onto variables
$variables{host} = $host if defined $host;

unless($no_connect){
        #prompt for host if not supplied
        unless (defined $host){$host = get_host();}

        #validate params
        unless($delay =~ /^[0-9]+$/){die "Invalid delay \n";}
        unless($timeout =~ /^[0-9]+$/){die "Invalid timeout \n";}
        unless($port =~ /^[0-9]+$/ && $port <= 65535){die "Invalid port \n";}
        unless($transport =~ /^(SSH|Telnet)$/){die "Invalid Transport (Must be SSH or Telnet) \n";}
        unless($host =~ /^((([01]?[0-9]{1,2}|[2][0-5]{1,2}).){3}([01]?[0-9]{1,2}|[2][0-5]{1,2}))$/){die "Invalid IP address\n";}


        #prompt for username if not supplied
        unless (defined $username){$username = get_username();}

        #add username to variables hash for our search and replace later
        $variables{username} = $username;

        #default for do_login is 1. 1= login required, 0=no login required and will go immediately to prompt.
        if($do_login){

                #prompt for password if not supplied
                unless (defined $password){
                        $password = get_password();
                }elsif($password =~ /^\d$/){
                        if ($password == 1) {
                                $password = 'super!man'
                        }elsif($password == 2){
                                $password = 'nvr1know'
                        }
                }

                #add password to variables hash for our search and replace later
                $variables{password} = $password;

        }else{
                $password = "";
        }


        #create our session depending on telnet or SSH
        if ($transport eq "SSH"){
          $s = Net::Appliance::Session->new({
             personality => $personality,
             transport => 'SSH',
             host => $host,
             do_privileged_mode => 0,
             do_configure_mode => 0,
             do_paging => 0,
             log_at => "debug",
             do_login => $do_login,
                          
             timeout => $timeout,
             SHKC    => 0, # SSH Strict Host Key Checking disabled
             nci_options => {
                                'library' => './personality',
                            } ,
             connect_options => {
                 opts => [
                     '-p', $port,             # connect to non-standard port
                     '-o', 'NumberOfPasswordPrompts=1',
                     '-o', 'LogLevel=ERROR',  # unknown what else this will supress but this will hide messages like "Warning: Permanently added 'x.x.x.x' (RSA) to the list of known hosts."
                 ],
             },
          });
        }elsif($transport eq "Telnet"){
          $s = Net::Appliance::Session->new({
             personality => $personality,
             transport => 'Telnet',
             host => $host,
             do_privileged_mode => 0,
             do_configure_mode => 0,
             do_paging => 0,
             wake_up => 1,
             log_at => "debug",
             do_login => $do_login,
             timeout => $timeout,
             nci_options => {'library' => './personality'} ,
             connect_options => {
                 port => $port,
             },
          });
        }else{
                die "Unsupported Transport\n";
        }

        ##### available log levels (emergency,alert,critical,error,warning,notice,info,debug)
        $s->set_global_log_at('debug') if $verbose || $debug;
}

# print additional variables if verbose or debug
if ($verbose || $debug || $check_chat_vars){
        print_verbose_variables();
}

##### Magic starts here
 try {
        my ($file_ref) = read_conversation_file();
        my ($file_required_variables_ref) = process_conversation_file($file_ref);
        my @file = @$file_ref;
        my @required_vars = @$file_required_variables_ref;

        #print Dumper(\%variables);

        #verify all required variables have been supplied
        my @missing;

        foreach (@required_vars){
                unless (defined $variables{$_}){
                        #die "Required Parameter not supplied: $_\n";
                        push(@missing, $_);
                }
        }

        my %seen;
        my @unique_missing = grep { ! $seen{$_}++ } @missing;
        if(@unique_missing){
                print_required_params(\@required_vars);
                #print_supplied_params(\%variables);
                print "Required Parameter(s) not supplied: ";
                print join("," , map {qq('$_')} @unique_missing);
                print "\n";
                exit 1;

        }

        unless($no_connect){
                $s->connect({ username => $username,
                              password => $password,
                });

                #this was noted in the documentation *not tested*
                if($personality eq "foundry"){$s->nci->transport->ors("\r\n");}
        }

        #handle the return index and order of anchors so we can go multiple deep and return back. (fake function like)
        my %return_index;
        my @path;



#Main loop begin
        for (my $i = 0; $i <= $#file; ++$i) {
                local $_ = $file[$i];
                chomp;
        print "Processing line: $_\n" if($debug);

                #skip if we are an anchor or comment or blank line
                next if m/^\s*(?:#|\s*$|::.+::$)/;

                # remove leading spaces and trailing whitespace and trailing comma if it exists
                s/\s*(.+?),?\s*$/$1/;


                while($_ =~/<!#(?!.*<!#)(.+?)#!>/){
                        my $key = $1;

                        if ($key =~ /:0$/){
                                my $realkey = $key;
                                $realkey =~ s/:0$//;
                                s/<!#$key#!>/$variables{$realkey}/;
                        }else{
                                s/<!#$key#!>/$variables{$key}/;
                        }

                }

                #ghetto solution to allow for escaping comma's
                s/\\,/#COMMA#/g;
                my @line = split(/,/, $_);
                @line = map { s/#COMMA#/,/g; $_ } @line;

                #remove leading and trailing slash on regex lines
                foreach (@line){
                        if(substr($_, 0 , 1) eq "/" && substr($_, length($_) - 1) eq "/"){
                                s/^\/(.*)\/$/$1/;
                        }
                }

                #process the return
                if ($line[0] eq "return") {
                        $i = $return_index{$path[$#path]} ;
                        pop @path;
                        next;
                }

###################
# type selection  #
###################

                elsif ($line[0] eq "send") {
        #SEND########## Send: commands
                        if($no_connect){die "The ability to use send is not allowed in local mode(-N)\n"}
                        my ($command,$prompt,$print,$cmd_timeout) = ($line[1],$line[2],$line[3],$line[4]);

                        my %options;
                        if ($cmd_timeout){$options{ 'timeout' } = $cmd_timeout;}
                        if ($prompt){$options{ 'match' } = qr/$prompt/;}

                        #run command
                        my @response = $s->cmd( $command, \%options);

                        #test error handling
                        #if ($@) {print err_handler($@);}

                        #remove command if it is the first element because I dont want to see it
                        if ($response[0] && $response[0] eq $command . "\n"){shift @response;}

                        #print response if requested
                        if ($print && $print eq 'print' && !$debug && !$verbose){
                                print @response;
                        #print response w/ no blank lines, added to pretty up devices that cannot turn off their paging feature
                        }elsif($print && $print eq 'print_nbl' && !$debug && !$verbose){
                                @response = grep { !/^\s*$/ } @response;
                                print @response;
                        }

                }elsif($line[0] eq "interactive"){
        #INTERACTIVE### Interactive mode. "Psuedo" . use "q!" to exit
                        my ($int_noblanklines) = ($line[1]);
                        if($no_connect){die "The ability to use interactive is not allowed in local mode(-N)\n"}

                        while(1){
                            my $last_prompt = $s->last_prompt;
#sometimes get back none printable/ascii text from bash prompt before prompt
#]0;foo@neoTokyo:~[foo@neoTokyo foo]$
#]00;foo@neoTokyo:~[foo@neoTokyo foo]$
                        $last_prompt =~ s/[^[:print:]]+//g;
                        if($last_prompt=~/~\[/){$last_prompt =~ s/.+?~(.+)/$1/;}

                            print $last_prompt ."\n\n";
                            print "'q!' to quit, command: ";
                            chomp(my $cmd = <STDIN>);
                                unless($cmd){next;}
                                if ($cmd eq "q!"){last;}
                                my @cmd = split(',', $cmd);
                                my %options;

                                if($cmd[0] eq "exit"){$s->close; exit;}

                                if($cmd[1]){$options{ 'match' } = qr/$cmd[1]/;}
                                my @temp = $s->cmd( $cmd[0], \%options);

                                #remove command if its the first element. happens on cisco.
                                if($temp[0] eq $cmd . "\n"){shift @temp;}

                                #remove blank lines if requested
                                if($int_noblanklines){@temp = grep { !/^\s*$/ } @temp;}
                                my $reply = join('',@temp);

                                #attempt to fix the blank lines added from the page breaks (works on cisco asa)
                                $reply =~ s/\n\n\s*\n/\n/g;
                                print $reply;
                        }

                }elsif($line[0] eq "grab"){
        #GRAB########## grab: a variable from results of previous command or variable
                        my ($grab_name,$grab_regex,$from_variable) = ($line[1],$line[2],$line[3]);
                        #grab which the match index
                        $grab_regex =~ m/\/:(\d+)$/;
                        my $grab_which_match;
                        if(looks_like_number($1)){
                           $grab_which_match = $1;
                        }else{
                           $grab_which_match = 0;
                        }
                        $grab_regex =~ s/:\d+$//;
                        $grab_regex =~ s/^\/(.*)\/$/$1/;

                        my $response;
                        my @matches;
                        if($from_variable){
                        #from_variable supplied so grab from it
                                $response = $variables{$from_variable};
                        }else{
                        #no variable supplied for grab from last send command
                                if($no_connect){die "The ability to grab from last command is not allowed in local mode(-N). Please supply a variable\n"}
                                my @response = $s->last_response;
                                $response = join('',@response);
                        }
                        push @matches, $1 while $response =~ /$grab_regex/smg;
                        unless($matches[$grab_which_match]){die "Failed to grab \"$grab_name\" variable\n";}
                        $matches[$grab_which_match] =~ s/,/\\,/g;
                        $variables{$grab_name} = $matches[$grab_which_match];

                        for (my $ii = 1; $ii <= @matches; $ii++) {
                                $variables{$grab_name.":".$ii} = $matches[$ii];
                        }

                }elsif($line[0] eq "check"){
        #CHECK######### check: if results match something expected
                        my ($check_var,$check_oper,$check_string,$check_pass_anchor,$check_fail_anchor) = ($line[1],$line[2],$line[3],$line[4],$line[5]);

                        $check_var =~ s/:0$//;
                        $check_string =~ s/^\/(.+)\/$/$1/;
                        die "no fail anchor provided: $_\n" unless($check_fail_anchor);
                        $return_index{$check_fail_anchor}=$i;
                        if($check_pass_anchor){$return_index{$check_pass_anchor}=$i;}
                        if($check_var){

                        #removes the quotes
                        $check_string =~ s/^"(.+)"$/$1/;
                        #populate variables hash
                        if(exists($variables{$check_var}) && defined($variables{$check_var})){
                        #if(exists($variables{$check_var}) && defined($variables{$check_var})){
                                if($check_pass_anchor){$variables{$check_pass_anchor} = $variables{$check_var};}
                                if($check_fail_anchor){$variables{$check_fail_anchor} = $variables{$check_var};}
                        }
                        ### variable supplied
                                if(exists($variables{$check_var})){
                                        #check_operation
                                        if($check_oper eq "re"){
                                                unless(defined $variables{$check_var} && $variables{$check_var} =~ m/$check_string/){
                                                        $i = find_anchor_index($check_fail_anchor,\@file);
                                                        push(@path, $check_fail_anchor);
                                                }else{
                                                        if($check_pass_anchor){
                                                                $i = find_anchor_index($check_pass_anchor,\@file);
                                                                push(@path, $check_pass_anchor);
                                                        }
                                                }
                                        }elsif($check_oper eq "!re"){
                                                unless(defined $variables{$check_var} && $variables{$check_var} !~ m/$check_string/){
                                                        $i = find_anchor_index($check_fail_anchor,\@file);
                                                        push(@path, $check_fail_anchor);
                                                }else{
                                                        if($check_pass_anchor){
                                                                $i = find_anchor_index($check_pass_anchor,\@file);
                                                                push(@path, $check_pass_anchor);
                                                        }
                                                }
                                        }elsif($check_oper eq "eq"){
                                                unless(defined $variables{$check_var} && $variables{$check_var} eq "$check_string"){
                                                        $i = find_anchor_index($check_fail_anchor,\@file);
                                                        push(@path, $check_fail_anchor);
                                                }else{
                                                        if($check_pass_anchor){
                                                                $i = find_anchor_index($check_pass_anchor,\@file);
                                                                push(@path, $check_pass_anchor);
                                                        }
                                                }
                                        }elsif($check_oper eq "gt"){
                                                unless(defined $variables{$check_var} && $variables{$check_var} > $check_string){
                                                        $i = find_anchor_index($check_fail_anchor,\@file);
                                                        push(@path, $check_fail_anchor);
                                                }else{
                                                        if($check_pass_anchor){
                                                                $i = find_anchor_index($check_pass_anchor,\@file);
                                                                push(@path, $check_pass_anchor);
                                                        }
                                                }
                                        }elsif($check_oper eq "lt"){
                                                unless(defined $variables{$check_var} && $variables{$check_var} < $check_string){
                                                        $i = find_anchor_index($check_fail_anchor,\@file);
                                                        push(@path, $check_fail_anchor);
                                                }else{
                                                        if($check_pass_anchor){
                                                                $i = find_anchor_index($check_pass_anchor,\@file);
                                                                push(@path, $check_pass_anchor);
                                                        }
                                                }
                                        }elsif($check_oper eq "gte"){
                                                unless(defined $variables{$check_var} && $variables{$check_var} >= $check_string){
                                                        $i = find_anchor_index($check_fail_anchor,\@file);
                                                        push(@path, $check_fail_anchor);
                                                }else{
                                                        if($check_pass_anchor){
                                                                $i = find_anchor_index($check_pass_anchor,\@file);
                                                                push(@path, $check_pass_anchor);
                                                        }
                                                }
                                        }elsif($check_oper eq "lte"){
                                                unless(defined $variables{$check_var} && $variables{$check_var} <= $check_string){
                                                        $i = find_anchor_index($check_fail_anchor,\@file);
                                                        push(@path, $check_fail_anchor);
                                                }else{
                                                        if($check_pass_anchor){
                                                                $i = find_anchor_index($check_pass_anchor,\@file);
                                                                push(@path, $check_pass_anchor);
                                                        }
                                                }
                                        }elsif($check_oper eq "ne"){
                                                unless(defined $variables{$check_var} && $variables{$check_var} ne "$check_string"){
                                                        $i = find_anchor_index($check_fail_anchor,\@file);
                                                        push(@path, $check_fail_anchor);
                                                }else{
                                                        if($check_pass_anchor){
                                                                $i = find_anchor_index($check_pass_anchor,\@file);
                                                                push(@path, $check_pass_anchor);
                                                        }
                                                }
                                        }else{
                                                die "Invalid check_operation against variable: $_\n";
                                        }
                                }else{
                                        die "Variable not found: $_ \n";
                                }
                        }else{
                        ### variable not supplied so use last response.
                                if($no_connect){die "The ability to check against last command response is not allowed in local mode(-N)\n"}
                                my @check_response = $s->last_response;
                                #adding prompt so we can check against it
                                push (@check_response , $s->last_prompt);
                                if($check_oper eq 're'){
                                        unless(grep m/$check_string/, @check_response){
                                                $i = find_anchor_index($check_fail_anchor,\@file);
                                                push(@path, $check_fail_anchor);
                                        }else{
                                                if($check_pass_anchor){
                                                        $i = find_anchor_index($check_pass_anchor,\@file);
                                                        push(@path, $check_pass_anchor);
                                                }
                                        }
                                }elsif($check_oper eq '!re'){
                                        if(grep m/$check_string/, @check_response){
                                                $i = find_anchor_index($check_fail_anchor,\@file);
                                                push(@path, $check_fail_anchor);
                                        }else{
                                                if($check_pass_anchor){
                                                        $i = find_anchor_index($check_pass_anchor,\@file);
                                                        push(@path, $check_pass_anchor);
                                                }
                                        }
                                # these dont make sense since we are dealing with multiple lines. placeholders for now.
                                #}elsif($check_oper eq 'eq'){
                                #}elsif($check_oper eq 'gt'){
                                #}elsif($check_oper eq 'lt'){
                                #}elsif($check_oper eq 'ne'){

                                }else{die "Invalid check_operation with no variable defined: $_\n";}
                        }

                }elsif($line[0] eq "wait" || $line[0] eq "sleep"){
        #WAIT########## wait: for x seconds
                        my ($wait_time) = ($line[1]);

                        sleep($wait_time);

                }elsif($line[0] eq "print"){
        #PRINT######### PRINT: a single line to the screen
                        my ($print_line) = ($line[1]);

                        $print_line =~ s/^"(.+)"$/$1/;
                        print "$print_line\n";

                }elsif($line[0] eq "printf"){
        #PRINTF######### PRINTF: a single sprintf line to the screen
                        my @printf_vars = $line[1];

                        #convert escaped chars into actual special characters
                        $printf_vars[0] = eval "qq#$printf_vars[0]#";

                        #variables start after printf and printf format
                        push(@printf_vars, @line[2 .. $#line]);

                        printf @printf_vars;

                }elsif($line[0] eq "do"){
        #DO############ do: a command or run a program
                        my ($cmd,$return_var,$print,$chomp) = ($line[1],$line[2],$line[3],$line[4]);

                        my $cmd_response = qx($cmd);
                                                     

                        if($chomp){chomp($cmd_response);}
                        if($print && $print eq "print"){print "$cmd_response";}
                        if($return_var){$variables{$return_var} = $cmd_response;}


                }elsif($line[0] eq "include"){
        #INCLUDE########## include: include another file (acts similar to a function)
                        #take varibles that can be passed to it. sounds like a good idea
                        my $include_file = $line[1];

                        #variables start after include and include_file
                        for (my $i=2; $i <= $#line; $i++){
                                $variables{$include_file.$i} = $line[$i];
                        }

                        $return_index{$include_file}=$i;
                        push(@path, $include_file);
                        $i = find_anchor_index($include_file,\@file);
                }elsif($line[0] eq "goto"){
        #GOTO########## goto: jump to anchor
                        my ($jump_anchor) = ($line[1]);

                        $return_index{$jump_anchor}=$i;
                        push(@path, $jump_anchor);
                        $i = find_anchor_index($jump_anchor,\@file);
                }elsif($line[0] eq "set"){
        #SET########## set: sets value of a variable
                        my ($set_variable,$set_find_value,$set_replace) = ($line[1],$line[2],$line[3]);
                        my $operation = substr($set_replace, 0 , 1);
                        if($set_find_value){
                                           
                                $variables{$set_variable}=~ s/$set_find_value/$set_replace/g;
                        }elsif ($operation eq "-"){
                                $set_replace = substr($set_replace, 1);
                                if( $variables{$set_variable} =~ m/^[0-9]+$/ && $set_replace =~ m/^[0-9]+$/ ){
                                        $variables{$set_variable}= $variables{$set_variable} - $set_replace;
                                }else{
                                        die "Cannot perform subtraction with non numeric variables: $variables{$set_variable} - " . $set_replace . "\n";
                                }
                        }elsif($operation eq "+"){
                                $set_replace = substr($set_replace, 1);
                                if( $variables{$set_variable} =~ m/^[0-9]+$/ && $set_replace =~ m/^[0-9]+$/ ){
                                        $variables{$set_variable}= $variables{$set_variable} + $set_replace;
                                }else{
                                        die "Cannot perform addition with non numeric variables: $variables{$set_variable} + " . $set_replace . "\n";
                                }
                        }else{
                                $set_replace =~ s/^"(.+)"$/$1/;
                                $variables{$set_variable}=$set_replace;
                        }

                }elsif($line[0] eq "write"){
        #WRITE######### write: writes to a file
                        my ($write_file,$write_variable,$nbl,$skip_cmd) = ($line[1],$line[2],$line[3],$line[4]);

                        unless(substr($write_file, 0 , 1) eq ">"){
                                #default to write/overwrite file
                                $write_file = ">" . $write_file;
                        }

                        unless($write_variable){
                                if($no_connect){die "The ability to write last command response is not allowed in local mode(-N)\n"}
                                my @last_response = $s->last_response;
                                if(defined $nbl){@last_response = grep { !/^\s*$/ } @last_response;}
                                if(defined $skip_cmd){shift @last_response;}
                                write_to_file("$write_file",\@last_response);
                        }else{
                                        if($write_variable =~ m/^".+"$/){
                                                $write_variable =~ s/^"(.+)"$/$1/;
                                                write_to_file("$write_file",$write_variable);
                                        }else{
                                                write_to_file("$write_file",$variables{$write_variable});

                                        }
                        }

                }elsif($line[0] eq "writef"){
        #WRITEF######### writef: writes to a file
                        my ($write_file,$write_format,$write_delimiter,$nbl,$skip_cmd) = ($line[1],$line[2],$line[3],$line[4],$line[5]);
                                                my @write_variables = @line[6 .. $#line];
                                                $write_format = eval "qq#$write_format#";

                        unless(substr($write_file, 0 , 1) eq ">"){
                                #default to write/overwrite file
                                $write_file = ">" . $write_file;
                        }

                        #print Dumper(\%variables);
                        unless(@write_variables){
                                if($no_connect){die "The ability to write last command response is not allowed in local mode(-N)\n"}
                                my @last_response = $s->last_response;
                                if(defined $nbl){@last_response = grep { !/^\s*$/ } @last_response;}
                                if(defined $skip_cmd){shift @last_response;}
                                writef_to_file("$write_file",\@last_response,$write_format,$write_delimiter);
                        }else{
                                for (my $i = 0; $i <= $#write_variables; $i ++){
                        if($write_variables[$i] =~ m/^".+"$/){
                                                $write_variables[$i] =~ s/^"(.+)"$/$1/;
                                        }
                                }
                                writef_to_file("$write_file",\@write_variables, $write_format, $write_delimiter);
                        }
                }elsif($line[0] eq "exit"){
        #EXIT########## exit: with message
                        my ($exit_message) = ($line[1]);

                        if(defined $exit_message){print "$exit_message\n";}
                        exit 0;
                }else {
        ############### DIE if unsupported type
                        die "Unsupported type specified \"$_\"\n";
                }

        if ($delay){sleep($delay);}
        }#next line / end main loop

        #close connection if established
        if (!$no_connect && $s->logged_in == 1) {$s->close;}

 }#end try
 catch { #catching and cleaning up errors, formating and removing parts of error messages I don't want such as perl module and line number and sometimes what it says.

        s/\sat\s.*//;
        s/^\n//;
        chomp($_);

        if (m/: No route to host/){
                print "$_\n";
        }
        elsif (m/^Permission denied/){
                print "$_\n";
        }
        elsif (m/^read timed-out$/){
                print "SSH: Connection failure: timed-out\n";
        }
        elsif ($transport eq 'Telnet' && $_ eq ''){
                print "Telnet: connect to host $host port $port No route to host \n";
        }
        else{
                #general failure
                #telnet does not say a user/pass failed so it will default to this with error "login failed to remote host - prompt does not match"
                print "Failure: $_\n";
        }
 }#end catch
 finally { #cleanup and close

        print Dumper($s) if $debug;

        #handy but might need its only option as this can include huge variables "command output"
        #print Dumper(\%variables) if $verbose >= 2;

        #error exit if error is passed over unless we processing a batch then we will just move on.
        if (@_ && !$csvfilename){exit 1;}

 };
$cur_loop++;
} while $cur_loop < $num_loops;

#clean exit
exit 0;

#########################################################################
# begin functions
#########################################################################

sub usage{
    print ("\nUsage: $program_name [-f path/to/conversation]\n\n");

    print ("   -H ip_address              - Host IP address to connect\n");
    print ("   -u username                - Username to login with. If ommited you will be prompted for username\n");
    print ("   -p # or password           - Which password variable to use or supply password. If ommited you will be prompted for password\n");
    print ("   -f path/to/conversation    - Path and filename of conversation *(Required)\n");
    print ("   -F path/to/csv             - Path and filename of csv file (this is a file that the program will loop through. ex: list of multiple device to run conversation on)\n");
    print ("                                - This file accepts comment lines.\n");
    print ("                                - the first non-comment line must start with \">\" and be a csv of variables (its the key)      ex: >host,username,password\n");
    print ("                                - the following lines must be a csv of variables that line up with the key ex: 10.1.2.3,cisco,cisco123\n");
    print ("   -c                         - Print required variables need by a conversation (-f)\n");
    print ("   -C                         - Print verbose variables used to run. (also printed with debug or verbose)\n");
    print ("   -D                         - Debug, more than verbose\n");
    print ("   -d seconds                 - delay between processing each line. slows conversation down.\n");
    print ("   -v                         - Verbose output. Good for troubleshooting conversation issues\n");
    print ("   -V var1,value1,var2,value2 - Variables. comma seperated key-value pairs key,value,key1,value1,etc....\n");
    print ("   -n                         - Disable password login, used for passwordless login (SSH keys), Only available for SSH\n");
    print ("   -N                         - Do not establish a connection (local mode)\n");
    print ("   -t                         - Set Transport to Telnet, Default is SSH if omitted\n");
    print ("   -T value                   - Set global command timeout value in seconds, Default is 30 seconds\n");
    print ("   -w personality             - Personality override. Default is to set personality to the last directory from -f option\n");
    print ("   -P value                   - Port override. Set port to custom value. default port is 22 for SSH and 23 for Telnet\n");

    print "\n";
    exit 0;
}

sub print_supplied_params{
        my $params = shift;
        print "Supplied parameter list:\n";
        foreach ( sort (keys %$params) ){
                #skip username and password since they are required 100% of the time
                unless ($_ eq 'username' || $_ eq 'password'){
                        print "\t". $_ ."\n";
                }
        }
        print "\n";
}

sub print_required_params{
        my $params = shift;
        print "Required parameter list:\n";
        my %seen;
        my @unique_params = grep { ! $seen{$_}++ } @$params;


        foreach (sort @unique_params){
                #skip username and password since they are required 100% of the time
                unless ($_ eq 'username' || $_ eq 'password'){
                        print "\t". $_ ."\n";
                }
        }
        print "\n";
}

sub print_verbose_variables{
        print "Debug: " . $debug . "\n";
        print "Delay: " . $delay . "\n";
        print "Verbose: " . $verbose . "\n";
        print "Variable: $_ = $variables{$_}\n" for (keys %variables);
        print "Trans: " . $transport . "\n";
        print "Username: " . $username . "\n" if $username;
        print "Port " . $port . "\n";
        print "Do_Login " . $do_login . "\n";
        print "Timeout: " . $timeout . "\n";
        print "No_Connect: " . $no_connect . "\n";
        print "Host: " . $host . "\n" if $host;
        print "Personality: " . $personality . "\n";
        print "Filename: " . $filename . "\n";
        print "CSV_Filename: " . $csvfilename . "\n\n\n";

}

sub get_host{
        print "Enter IP address: ";
        chomp(my $host_ip=<STDIN>);
        print "\n";
        return($host_ip);

}

sub get_username{
        print "Enter login Username: ";
        chomp(my $uname=<STDIN>);
        print "\n";
        return($uname);

}

sub get_password{
        print "Enter login Password: ";
        system('stty','-echo'); #Hide console input for what we type
        chomp(my $pass=<STDIN>);
        system('stty','echo'); #Unhide console input for what we type
        print "\n\n";
        return($pass);
}

sub write_to_file{
        my $write_file = shift;
        my $value = shift;

        open (WRITEFILE, "$write_file") || die "Failed to open file: $write_file\n";
        if( ! ref($value)){
                print WRITEFILE $value . "\n";
        }elsif(ref($value) eq "ARRAY"){
                print WRITEFILE @$value;
        }else{
                die "Unsupported reference\n";
        }
        close (WRITEFILE);
}
sub writef_to_file{
        my $write_file = shift;
        my $value = shift;
        my $write_format = shift;
        my $write_delimiter = shift;

        my $printf_count = &check_format_count($write_format);

        open (WRITEFILE, "$write_file") || die "Failed to open file: $write_file\n";
        if(ref($value) eq "ARRAY"){
                if ($write_delimiter) {
                        my @delimited_array;
                        my $d_array_count;
                        foreach my $tmp_value (@$value){
                                @delimited_array = split ("$write_delimiter", $tmp_value, $printf_count) ;
                                $d_array_count = $#delimited_array + 1;
                        }
                        if ($d_array_count >= $printf_count){
                                printf WRITEFILE $write_format, @delimited_array;
                        } else {
                                print WRITEFILE @$value;
                                print WRITEFILE "\n";
                        }
                } else {
                        printf WRITEFILE $write_format, @$value;
                }
        }else{
                die "Unsupported reference\n";
        }
        close (WRITEFILE);
}
sub check_format_count{
        my $format = shift;

        my $format_number = () = $format =~ /\%(?:[\d\.\-]+)?[csduoxefgXEGpn]/g;
        return $format_number;
}
sub read_list_file{
        my @list_file;
        my $var_count;
        my $definition_found = 0;

        if($csvfilename){
                open( MYFILE, "<", $csvfilename ) || die "Can't open $csvfilename: $!\n";
                while (<MYFILE>) {
                        chomp;
                        #skip comments and blank lines
                        if(m/^#/){next;}
                        if(m/^\s*$/){next;}
                        #ghetto comma escaping!
                        s/\\,/#COMMA#/g;
                        my @line = split(/,/ , $_);

                        #this will be the variable definition line. aka. list and order of variables
                        #needs to be the first line that is not a comment or blank line
                        if($line[0] =~ m/^>/) {
                                $definition_found = 1;
                                $var_count = scalar(@line);
                        }

                        if ($definition_found == 0){die "list file is missing the definition line aka \">\" line that defines the variables (needs to be first non-comment line)\n";}
                        unless($var_count == scalar(@line)){die "$csvfilename file variable count error, should be $var_count variables in line: $_\n";}
                        push(@list_file, $_);
                }
                close(MYFILE);
        }
        return(\@list_file);
}

sub read_conversation_file{
        open( MYFILE, "<", $filename ) || die "Can't open $filename: $!\n";
        my @file;
        my @include_files;
        while (<MYFILE>) {
                my @line = split(/,/ , $_);
                if(m/^include,/){
                        chomp($line[1]);
                        push(@include_files, $line[1]);
                }
                chomp;
                push(@file, $_);
        }
        close(MYFILE);


#process include files and add them to the mix
#
        my %filetrack;
        foreach (@include_files){

                my $filename = $_;

                #only process include file once
                if($filetrack{$filename}){
                        next;
                }else{
                        $filetrack{$filename} = 1;
                }
                my $fullfilename = "./include/$filename";
                open( INCLUDE, "<", $fullfilename ) || die "Can't open Include file $fullfilename: $!\n";
                push (@file, "");
                push (@file, "::".$filename."::");

                while (<INCLUDE>){
                        chomp;
                        s/<!#\$(\d)#!>/<!#$filename$1#!>/g;
                        push(@file, $_);
                }
        }

        #print Dumper(\@file);

        return(\@file);
}

sub process_conversation_file{
        my $conversation_array_ref = shift;

        my @required_vars;
        my @grab_vars;
        foreach (@$conversation_array_ref) {
                next if (m/^\s*#/);
                push(@required_vars, ( m/<!#([\w\d]+)#!>/g ));
                my @line = split(/,/, $_);
                if(m/^grab,/){
                        push(@grab_vars, $line[1]);
                }elsif(m/^do,/){
                        if($line[2]){push(@grab_vars, $line[2]);}
                }elsif(m/^check,/){
                        if($line[4]){push(@grab_vars, $line[4]);}
                        if($line[5]){push(@grab_vars, $line[5]);}
                }elsif(m/^set,/){
                        if($line[1]){push(@grab_vars, $line[1]);}
                }
        }
        close(MYFILE);

        foreach my $grab (@grab_vars){
                chomp($grab);
                @required_vars = grep {$_ ne $grab} @required_vars;
        }
        return(\@required_vars);


}


sub find_anchor_index{
        my $anchor = shift;
        my $arrayref = shift;
        my @array = @$arrayref;
        my $i = first {$array[$_] =~  /^\:\:$anchor\:\:$/} 0..$#array;
        if($i == 0){die "Could not find anchor: $anchor\n";}
        return($i);

}

sub err_handler {
     my $err  = shift;

     my $message;
     my $errmsg;
     my $lastline;

     if ( UNIVERSAL::isa($@, 'Net::Appliance::Session::Exception') ) {

         # fault description from Net::Appliance::Session
         $message  = $@->message;
         $errmsg   = $@->errmsg;
         $lastline = $@->lastline;

         return $message . "$errmsg \n" .  "$lastline \n";
     }
     else {

         # error from Net::Appliance::Session or $@
         $message  = $@;

         return $message;
     }
 }

