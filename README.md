# The Unachievable Ideal of Chibi Elf Grunting Noises When They Get Punched: A Z3 Rom Patcher
## Aka: "z3_oof_patcher"

Patches an expanded ALTTP Japanese v1.0 ROM to replace Link's "oof" sound effect with a custom version. By default, the patch uses a feminine voice effect.

## How to use:

1. If you want to use your own sound, you will need a new .brr file encoding the sound. To get this, I recommend using this tool (https://github.com/boldowa/snesbrr) and using the `--encode` function on a 16-bit signed PCM .wav at 12khz. The .brr file cannot exceed 2672 bytes, which equates to a maximum of 296 blocks. The longest .wav file you can use is therefore around 0.394s. As for getting the .wav with the right specs, I recommend Audacity. You could edit a sample you've obtained elsewhere, or use your own. Grunt into your mic to your heart's content!
2. You will need a (legally obtained) ALTTP Japanese version v1.0 ROM (expanded to 2mb).
3. To use the default feminine voice (`default.brr`), simply place your .sfc file by itself in the same directory as the executable and run it. Alternatively, run via command line with the same arguments below, but omit the `--brr` argument.
4. To use a custom sample, run the executable via command line with `--rom romFilename --brr sampleFilename --output outputFilename` replacing the arguments with your file names.

The patcher does not do any checks on the rom aside from a basic header check and file size check. That is to say, if any other mods or patches touch the same bytes, this patch will conflict with it. Please note any issues with compatibility and I will do what I can to accomodate.

Note that this patcher does not currently bother to fix the checksum. I may or may not get around to doing that.

## Arguments:

`--rom romFilename`: The rom file to patch. If not specified, the program will attempt to apply the default patch to the first .sfc file in the directory.

`--brr sampleFilename`: (Optional) The 576 byte brr-encoded sample file. If not specified, `default.brr` will be used. This is included in release but you can of course overwrite it with whatever you prefer to use.

`--output outputFilename`: (Optional) A valid filename. If not specified, the output file will be called `"patched_"` + the input filename.

## How this works:

An additional .brr sample the same size as the original "oof" sound is embedded into the rom. On boot, it is then loaded into SPC memory and only played when Link takes damage, thus leaving the old sample untouched (otherwise the cucco sound would also be modified).

## Does it affect race timings?

No; all CPU load takes place at the initial boot phase and everything following that is identical instructions. The sound switching is done by the SPC, and works by simply changing pointers at the correct moments (i.e. - negligible SPC load).

## High-level explanation of changes:

A patch is applied which alters the SPC data load routine to insert the new sample at the largest contiguous block in SPC memory; i.e. $3188. This space is technically used by the credits song at the end of the game, but since at that point we don't care about the "oof" sound anymore, we can use it freely.

Via `witch princess kan`:

```The sample is assigned to sound effect instrument 09, which is the only predefined instrument not used by any of the existing sound effects. The instrument assigned to SFX2.26 is then set to 09 to make use of the new sound effect without corrupting the noise chickens make.```

(Credit to witch princess kan for this approach. It's more elegant than my previous method, which had the SPU dynamically altering pointer tables and required modifying base code.)

## What's with the project name?

When I first proposed this idea on the ALTTPR discord, a dev there (*cough*) had some choice words for me and my idea. In their honor, I have named this project according to their flowery descriptions thereof.
