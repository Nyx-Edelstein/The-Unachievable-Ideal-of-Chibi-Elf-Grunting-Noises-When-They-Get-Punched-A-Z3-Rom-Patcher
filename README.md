# The Unachievable Ideal of Chibi Elf Grunting Noises When They Get Punched: A Z3 Rom Patcher
## Aka: "z3_oof_patcher"

Patches an expanded ALTTP Japanese v1.0 ROM to replace Link's "oof" sound effect with a custom version. By default, the patch uses a feminine voice effect.

## How to use:

1. If you want to use your own sound, you will need a new .brr file exactly 576 bytes in length. To get this, I recommend using this tool (https://github.com/boldowa/snesbrr) and using the `--encode` function on a 16-bit signed PCM .wav at 12khz with an exact length of 0.085s. As for getting the .wav with the right specs, I recommend Audacity.
2. You will need a (legally obtained) ALTTP Japanese version v1.0 ROM (expanded to 2mb).
3. To use the default (feminine) voice, simply place your .sfc file by itself in the same directory as the executable and run it. Alternatively, run via command line with the same arguments below, but omit the `--brr` argument.
4. To use a custom sample, run the executable via command line via `--rom romFilename --brr sampleFilename --output outputFilename` replacing the arguments with your file names.

The patcher does not do any checks on the rom aside from a basic header check and file size check. That is to say, if any other mods or patches touch the same bytes, this patch will conflict with it. Please note if there are any issues with compatibility and I will do what I can to accomodate.

Note that this patcher does not currently bother to fix the checksum. I may or may not get around to doing that.


## How this works:

An additional .brr sample the same size as the original "oof" sound is embedded into the rom. On boot, it is then loaded into SPC memory and only played when Link takes damage, thus leaving the old sample untouched (otherwise the cucco sound would also be modified).

## Does it affect race timings?

No; all CPU load takes place at the initial boot phase and everything following that is identical instructions. The sound switching is done by the SPC, and works by simply changing pointers at the correct moments (i.e. - negligible SPC load).

## High-level explanation of changes:

The SPC load routine, starting at 0x888, is relocated to 0x1C8888 to give room to expand. The logic of the load routine is quite complex and highly optimised, but it essentially serves to load in all of the sound data into SPU memory at game boot. It does this in chunks by communicating to the SPC the number of bytes in the chunk and the address in SPC memory where that data should go, and then loops through, sending the bytes in synchronicity over the CPU/SPC I/O channels and waiting for the SPC to ack at every step.

The main issue was that there was not enough room in the ROM where the SPC load data is stored to embed a new sample. Thus the sample had to be located elsewhere and the load routine modified to include it. Fortunately on the SPC side, there was plenty of unused memory directly following where the main data is stored (i.e. starting at 0xBAA0 in SPC memory).

The changes I made to the routine are a bit on the bodgey side, but the logic is: detect when data transfer is complete but the CPU has not yet signaled to the SPC to cease loading and begin normal execution. At this point, load a new chunk that contains all the necessary data, then recover state and resume execution.

The new chunk also contains an SPC subroutine modification starting at 0x1CF3C4 (the old routine, loaded from 0xCFCBE / 0x08F0 in SPU memory, has been moved here). As mentioned earlier, the only function of this modification is to detect when the "oof" sound should be played, and then change pointers to use the new sound sample.
