# FortiGate Scripts Not Converted

The following dudescript files could not be converted because they use local command execution (`do` command) or file operations (`write` command) which are not supported in the current scripting implementation:

## clearblocks
**Reason:** Uses `do` and `write` commands to execute local shell commands
- Writes routes to a temporary file
- Uses shell commands like `sed` to process output
- Loops using goto/labels (could be converted to while loop)

**Workaround:** You could manually delete blocks by running the block listing script first, then using unblock_ip.yaml for each block.

## listblocks
**Reason:** Uses `do` and `write` commands
- Writes to temporary file
- Uses `sed` to format output
- Runs `cat` and shell piping

**Workaround:** Use this simple alternative:
```yaml
---
name: List Blocks (Simple)
steps:
  - send: sh firewall addrgrp FSM_BLOCK_GROUP
    capture: output
  - print: "Current blocks in FSM_BLOCK_GROUP:"
  - print: "${output}"
```

## smart_block_test
**Reason:** Uses Perl script for interface detection
- Calls `perl ./conversation/fortigate/programs/find_interface.pl` to match IP to routing table
- Writes routing table to temp file
- Computes policy ID using md5sum

**Workaround:** Use block_ip.yaml with pre-determined interface and policy_id values from your CSV.

## smart_unblock_test
**Reason:** Uses Perl script for interface detection
- Same as smart_block_test - requires local Perl execution
- Computes policy number using md5sum

**Workaround:** Use unblock_ip.yaml with pre-determined policy_id from your CSV.

---

## Future Consideration

If local command execution is added to the scripting language in the future, these scripts could be fully converted. The `do` command would need to support:
- Running arbitrary shell commands
- Capturing output to variables
- Piping input to commands
