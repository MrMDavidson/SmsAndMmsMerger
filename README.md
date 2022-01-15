# SmsAndMmsMerger

## What is it?

This small command line application will take multiple backups from [SynTech's SMS Backup & Restore](https://synctech.com.au/sms-backup-restore/) application and merge them into one file. It'll attempt to deduplicate items across the file set.

## Why, though?

I bought a new phone and used Android's built in SMS/MMS restore. Everything seemed fine and it restored 10 years worth of SMS and MMS content. It wasn't until a month or so later that I realised it hadn't actually restored any of my MMS content; I was left with empty place holder messages. Years worth of memories of pets, friends, and family gone. I engaged with Google's support which was less than helpful. In the meantime I continued to receive SMS and MMS content. I now had a dilemma - if I continued as is I'd lose years worth of memories. If I didn't I'd lose those new memories. Luckily my old phone still worked so I could capture a backup of the content from that phone. However due to the way the native Google application had restoed the content a simple restore from SynTech's application would not recover the content as it was under the impression the messages had been restored.

Being an engineer, and a data hoarder, I set about fixing the problem.

## Concepts

### Backup File Ordering

This relies on the concept of having a preferred order for all your backups. It'll work with any number of backups greater than 1. But if it finds an SMS/MMS in more than one of these files it needs to know which version to use. In my scenario I had `oldphone.xml` and `newphone.xml`. Most of my content was in both of these files, but was corrupt in `newphone.xml`. So my preferred order was;
  1. `oldphone.xml`
  1. `newphone.xml`

However you might have a situation where you weren't able to restore your old phone and so they're pretty much distinct. But if there were overlap you'd prefer your new phone's file to be used instead;
  1. `newphone.xml`
  1. `oldphone.xml`

Or maybe you have a series of backups for your phone but you want to merge them. You'd probably prefer the newest file first.
  1. `backup-2022-01-15.xml`
  1. `backup-2022-01-08.xml`
  1. `backup-2022-01-01.xml`

## Building it

The application is written in C# for .Net 6.0. You'll need the SDK and runtime for that installed. You can fetch those from the [.Net 6.0 Download site](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

Afer that it's straight forward

  1. `dotnet restore`
  1. `dotnet build`

## Running it

You can get help by running `SmsAndMmsMerger` without arguments or with `--help` - `SmsAndMmsMerger --help`

My example use case of prefering content from my old phone would be merged using;
`SmsAndMmsMerger -i newphone.xml,oldphone.xml -o merged.xml`

### Arguments

| Parameter             | Short | Long                    | Explanation                                                                                                                                                                                                                                                                                                                                                          | Example                                                            |
|-----------------------|-------|-------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------|
| Input File            | -i    | --input-file            | Ordered list of files to merge content from                                                                                                                                                                                                                                                                                                                          | -i LowPriorityFile.xml,MediumPriorityFile.xml,HighPriorityFile.xml |
| Output File           | -o    | --output-file           | File to write merged results to                                                                                                                                                                                                                                                                                                                                      | -o merged.xml                                                      |
| Match Addresses       |       | --must-match-address    | A list of addresses that SMS/MMS must be to/from to exist in the merged file. Use it to merge only a handful of conversations or to test the merge                                                                                                                                                                                                                   | --must-match-address 412345678,487654321                           |
| Force output          | -f    | --force                 | If not specified will abort if the `--output-file` already exists                                                                                                                                                                                                                                                                                                    |                                                                    |
| Reorder Items by Date |       | --reorder-items-by-date | "SMS Backup & Restore" generates files that are all SMSes and then all the MMSes. When restoring these files it can lead to misleading restore time estimations as SMSes restore a lot quicker than an MMS. Specify this flag to intermingle MMS and SMS in the output file based on the date they were received. Gives greater accuracy to restore time estimations |                                                                    |

### Technical Notes

The application uses memory mapped files to read the source content and a forward only XMLReader. It's been tested with files over 2.5gb in size and performs fine (run time of about 30 seconds). However it can consume a reasonable amount of memory depending on how many of input files you specify and how many MMS/SMS files you have in them. For my use case of merging a 2.5gb file containing SMS/MMS and a 200mb file containing (effectively only) SMS it takes about 30 seconds to run and consumed about 500mb of memory. Your mileage may vary.