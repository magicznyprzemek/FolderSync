# FolderSync
One-way folder synchronization at a fixed interval.

## Usage
'dotnet run -- --source <path> --replica <path> --interval <seconds> --log <logfile> [--hash]'

'--hash' enables MD5 content verification

## Example:
dotnet run -- --source /Users/.../test --replica /Users/.../test_replica --interval 30 --log /Users/.../sync.log
