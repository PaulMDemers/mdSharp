using MdSharp.Core;
using MdSharp.Core.Audio;
using MdSharp.Core.Bus;
using MdSharp.Core.Cartridge;
using MdSharp.Core.Cpu.Sh2;
using MdSharp.Core.Cpu.Z80;
using MdSharp.Core.Input;
using MdSharp.Core.State;
using MdSharp.Core.ThirtyTwoX;
using MdSharp.Core.Video;
using System.Runtime.CompilerServices;

int failures = 0;

Run("cartridge header parsing", CartridgeHeaderParsing);
Run("cartridge diagnostics", CartridgeDiagnosticsReport);
Run("32X hardware profile", ThirtyTwoXHardwareProfileReport);
Run("32X device shell", ThirtyTwoXDeviceShell);
Run("32X SH-2 FRT input capture signal", ThirtyTwoXSh2FrtInputCaptureSignal);
Run("32X SH-2 FRT counter writes and flags", ThirtyTwoXSh2FrtCounterWritesAndFlags);
Run("32X SH-2 SCI byte transfer", ThirtyTwoXSh2SciByteTransfer);
Run("32X SH-2 core executes synthetic code", ThirtyTwoXSh2CoreExecutesSyntheticCode);
Run("32X SH-2 BRA self idle loop fast-forward", ThirtyTwoXSh2BraSelfIdleLoopFastForward);
Run("32X SH-2 ADD BRA NOP delay loop fast-forward", ThirtyTwoXSh2AddBraNopDelayLoopFastForward);
Run("32X SH-2 DT/BF delay loop fast-forward", ThirtyTwoXSh2DtBfDelayLoopFastForward);
Run("32X SH-2 NOP DT/BF delay loop fast-forward", ThirtyTwoXSh2NopDtBfDelayLoopFastForward);
Run("32X SH-2 MOV.L ADD BF/S DT loop fast-forward", ThirtyTwoXSh2MovLAddBfSDtLoopFastForward);
Run("32X SH-2 MOV.L NOP DT BF/S ADD loop fast-forward", ThirtyTwoXSh2MovLNopDtBfSAddLoopFastForward);
Run("32X SH-2 MOV.W DT BF/S ADD fill loop fast-forward", ThirtyTwoXSh2MovWStoreDtBfSAddLoopFastForward);
Run("32X SH-2 MOV.W ADD register DT BF ramp loop fast-forward", ThirtyTwoXSh2MovWStoreAddRegisterDtBfLoopFastForward);
Run("32X SH-2 word table search loop fast-forward", ThirtyTwoXSh2WordTableSearchLoopFastForward);
Run("32X SH-2 byte fill indexed CMP/GE loop fast-forward", ThirtyTwoXSh2ByteFillIndexedCmpGeFastForward);
Run("32X SH-2 word high-bit mask transform fast-forward", ThirtyTwoXSh2WordHighBitMaskTransformFastForward);
Run("32X SH-2 word high-bit mask transform outer fast-forward", ThirtyTwoXSh2WordHighBitMaskTransformOuterFastForward);
Run("32X SH-2 byte lookup word row expand fast-forward", ThirtyTwoXSh2ByteLookupWordRowExpandFastForward);
Run("32X SH-2 byte lookup word store step fast-forward", ThirtyTwoXSh2ByteLookupWordStoreStepFastForward);
Run("32X SH-2 MOV literal TST/BF poll loop fast-forward", ThirtyTwoXSh2MovLiteralTstBfPollLoopFastForward);
Run("32X SH-2 MOV literal long TST/BT poll loop fast-forward", ThirtyTwoXSh2MovLiteralLongTstBtPollLoopFastForward);
Run("32X SH-2 MOV literal word TST/BT poll loop fast-forward", ThirtyTwoXSh2MovLiteralWordTstBtPollLoopFastForward);
Run("32X SH-2 MOV literal word CMP/EQ BT poll loop fast-forward", ThirtyTwoXSh2MovLiteralWordCmpEqBtPollLoopFastForward);
Run("32X SH-2 MOV literal byte CMP/EQ BT poll loop fast-forward", ThirtyTwoXSh2MovLiteralByteCmpEqBtPollLoopFastForward);
Run("32X SH-2 word CMP/EQ BT poll loop fast-forward", ThirtyTwoXSh2WordCmpEqBtPollLoopFastForward);
Run("32X SH-2 stable word pair CMP/EQ BT poll loop fast-forward", ThirtyTwoXSh2StableWordPairCmpEqBtPollLoopFastForward);
Run("32X SH-2 long register CMP/EQ BT poll loop fast-forward", ThirtyTwoXSh2LongRegisterCmpEqBtPollLoopFastForward);
Run("32X SH-2 word CMP/EQ BF poll loop fast-forward", ThirtyTwoXSh2WordCmpEqBfPollLoopFastForward);
Run("32X SH-2 word displacement TST BT poll loop fast-forward", ThirtyTwoXSh2WordDisplacementTstBtPollLoopFastForward);
Run("32X SH-2 padded long TST BT poll loop fast-forward", ThirtyTwoXSh2LongTstBtPaddedPollLoopFastForward);
Run("32X SH-2 long masked change BT/S delay poll loop fast-forward", ThirtyTwoXSh2LongMaskedChangeBtSDelayPollLoopFastForward);
Run("32X SH-2 GBR long masked OR compare BF poll loop fast-forward", ThirtyTwoXSh2GbrLongMaskedOrCompareBfPollLoopFastForward);
Run("32X SH-2 word increment GBR zero BT poll loop fast-forward", ThirtyTwoXSh2WordIncrementGbrZeroBtPollLoopFastForward);
Run("32X SH-2 word TST BT poll loop fast-forward", ThirtyTwoXSh2WordTstBtPollLoopFastForward);
Run("32X SH-2 word TST BF poll loop fast-forward", ThirtyTwoXSh2WordTstBfPollLoopFastForward);
Run("32X SH-2 byte TST BF poll loop fast-forward", ThirtyTwoXSh2ByteTstBfPollLoopFastForward);
Run("32X SH-2 byte displacement TST immediate BT poll loop fast-forward", ThirtyTwoXSh2ByteDisplacementTstImmediateBtPollLoopFastForward);
Run("32X SH-2 peripheral byte TST immediate poll fast-forwards in device", ThirtyTwoXSh2PeripheralByteTstImmediatePollFastForward);
Run("32X SH-2 byte displacement zero wait DT/BF loop fast-forward", ThirtyTwoXSh2ByteDisplacementZeroWaitDtBfLoopFastForward);
Run("32X SH-2 TST BF/S delay ADD loop fast-forward", ThirtyTwoXSh2TstBfsDelayAddLoopFastForward);
Run("32X SH-2 two-stage word poll ring fast-forward", ThirtyTwoXSh2TwoStageWordPollRingFastForward);
Run("32X SH-2 SDRAM flag tasklet fast-forward", ThirtyTwoXSh2SdramFlagTaskletFastForward);
Run("32X SH-2 SDRAM flag tasklet dispatcher loop fast-forward", ThirtyTwoXSh2SdramFlagTaskletDispatcherLoopFastForward);
Run("32X SH-2 GBR byte-pair tasklet fast-forward", ThirtyTwoXSh2GbrBytePairTaskletFastForward);
Run("32X SH-2 GBR byte-pair interrupt idle loop fast-forward", ThirtyTwoXSh2GbrBytePairInterruptIdleLoopFastForward);
Run("32X SH-2 GBR byte zero BT poll loop fast-forward", ThirtyTwoXSh2GbrByteZeroBtPollLoopFastForward);
Run("32X SH-2 literal byte displacement TST register BT poll loop fast-forward", ThirtyTwoXSh2LiteralByteDisplacementTstRegisterBtPollLoopFastForward);
Run("32X SH-2 MOV.W swap copy loop fast-forward", ThirtyTwoXSh2MovWordSwapCopyLoopFastForward);
Run("32X SH-2 MOV.W strided copy loop fast-forward", ThirtyTwoXSh2MovWordStridedCopyLoopFastForward);
Run("32X SH-2 empty descriptor span fill fast-forward", ThirtyTwoXSh2EmptyDescriptorSpanFillFastForward);
Run("32X SH-2 long difference poll fast-forward", ThirtyTwoXSh2LongDifferencePollFastForward);
Run("32X SH-2 framebuffer word fill loop fast-forward", ThirtyTwoXSh2FrameBufferWordFillLoopFastForward);
Run("32X SH-2 SDRAM mirrors", ThirtyTwoXSh2SdramMirrors);
Run("32X SH-2 GBR CMP/EQ BF poll loop fast-forward", ThirtyTwoXSh2GbrCmpEqBfPollLoopFastForward);
Run("32X SH-2 GBR CMP/EQ BT poll loop fast-forward", ThirtyTwoXSh2GbrCmpEqBtPollLoopFastForward);
Run("32X SH-2 GBR register CMP/EQ BF poll loop fast-forward", ThirtyTwoXSh2GbrRegisterCmpEqBfPollLoopFastForward);
Run("32X SH-2 null linked-list idle loop fast-forward", ThirtyTwoXSh2NullLinkedListIdleLoopFastForward);
Run("32X SH-2 GBR word CMP/GT BF poll loop fast-forward", ThirtyTwoXSh2GbrWordCmpGtBfPollLoopFastForward);
Run("32X SH-2 padded GBR CMP/EQ BF/BRA poll loop fast-forward", ThirtyTwoXSh2PaddedGbrCmpEqBfBraPollLoopFastForward);
Run("32X SH-2 linked-list insert fast-forward matches interpreter", ThirtyTwoXSh2LinkedListInsertFastForwardMatchesInterpreter);
Run("32X SH-2 DMA transfer size bits", ThirtyTwoXSh2DmaTransferSizeBits);
Run("32X SH-2 arithmetic flags", ThirtyTwoXSh2ArithmeticFlags);
Run("32X user header loads initial program", ThirtyTwoXUserHeaderLoadsInitialProgram);
Run("32X adapter control and communication ports", ThirtyTwoXAdapterControlAndCommunicationPorts);
Run("32X packed pixels use full palette index", ThirtyTwoXPackedPixelsUseFullPaletteIndex);
Run("32X cached framebuffer bytes map before cartridge ROM", ThirtyTwoXCachedFrameBufferBytesMapBeforeCartridgeRom);
Run("32X fixed cartridge cache tags use SH-2 address", ThirtyTwoXFixedCartridgeCacheTagsUseSh2Address);
Run("32X packed palette zero is transparent", ThirtyTwoXPackedPaletteZeroIsTransparent);
Run("32X communication byte read/write edge", ThirtyTwoXCommunicationByteReadWriteEdge);
Run("32X 68000 system handshakes sync SH-2", ThirtyTwoXM68kSystemHandshakesSyncSh2);
Run("32X SH-2 watchdog keyed writes", ThirtyTwoXSh2WatchdogKeyedWrites);
Run("32X SH-2 watchdog interval interrupt", ThirtyTwoXSh2WatchdogIntervalInterrupt);
Run("32X SH-2 division unit", ThirtyTwoXSh2DivisionUnit);
Run("32X PWM interrupts advance with executed SH-2 cycles", ThirtyTwoXPwmInterruptsAdvanceWithExecutedSh2Cycles);
Run("32X 68000 bus shell", ThirtyTwoXM68kBusShell);
Run("32X 68000 vector ROM mapping", ThirtyTwoXM68kVectorRomMapping);
Run("32X 68000 VBlank uses Genesis level 6", ThirtyTwoXM68kVBlankUsesGenesisLevel6);
Run("32X SH-2 boot ROM ready marker", ThirtyTwoXSh2BootRomReadyMarker);
Run("32X SH-2 boot ROM maps optional BIOS images", ThirtyTwoXSh2BootRomMapsOptionalBiosImages);
Run("32X SH-2 real BIOS boot uses reset vectors", ThirtyTwoXSh2RealBiosBootUsesResetVectors);
Run("68k reset and simple instructions", CpuResetAndSimpleInstructions);
Run("68k Codemasters DNLD reset vectors", CodemastersDnldResetVectors);
Run("VDP register and VRAM writes", VdpRegisterAndVramWrites);
Run("VDP byte writes mirror onto the 16-bit data bus", VdpByteWritesMirrorOntoDataBus);
Run("VDP command decode and history", VdpCommandDecodeAndHistory);
Run("VDP HV counter and interrupt status", VdpHvCounterAndInterruptStatus);
Run("VDP V interrupt pending survives frame boundary until status read", VdpVInterruptPendingSurvivesFrameBoundaryUntilStatusRead);
Run("VDP 68k interrupt acknowledge clears pending flags", VdpM68kInterruptAcknowledgeClearsPendingFlags);
Run("VDP HV counter advances horizontally", VdpHvCounterAdvancesHorizontally);
Run("VDP HBlank status pulses without H interrupt", VdpHBlankStatusPulsesWithoutHInterrupt);
Run("VDP 68k DMA copy writes VRAM and CRAM", VdpDmaMemoryCopyWritesVramAndCram);
Run("VDP long DMA timing scales with transfer length", VdpLongDmaTimingScalesWithTransferLength);
Run("VDP DMA fill and copy modes", VdpDmaFillAndCopyModes);
Run("VDP DMA only starts on completed command", VdpDmaOnlyStartsOnCompletedCommand);
Run("VDP data FIFO adds 68k wait cycles when full", VdpDataFifoAdds68kWaitCyclesWhenFull);
Run("68000 peripheral accesses add wait cycles", M68kPeripheralAccessesAddWaitCycles);
Run("controller TH multiplexing", ControllerMultiplexing);
Run("six-button controller handshake", SixButtonControllerHandshake);
Run("controller data and control ports", ControllerDataAndControlPorts);
Run("controller input pins do not drive TH", ControllerInputPinsDoNotDriveTh);
Run("Sega Team Player adapter protocol", SegaTeamPlayerAdapterProtocol);
Run("EA 4-Way Play adapter protocol", Ea4WayPlayAdapterProtocol);
Run("light gun adapter button protocol", LightGunAdapterButtonProtocol);
Run("input movies preserve separate player inputs", InputMoviesPreserveSeparatePlayerInputs);
Run("input movies load legacy single-player frames", InputMoviesLoadLegacySinglePlayerFrames);
Run("hardware version register reflects region", HardwareVersionRegisterReflectsRegion);
Run("PSG reset starts muted", PsgResetStartsMuted);
Run("PSG tone generation", PsgToneGeneration);
Run("PSG writes render at frame timestamps", PsgWritesRenderAtFrameTimestamps);
Run("PSG frame events are sorted by timestamp", PsgFrameEventsAreSortedByTimestamp);
Run("PSG channel stems isolate tone channel", PsgChannelStemsIsolateToneChannel);
Run("PSG snapshots expose tone state", PsgSnapshotsExposeToneState);
Run("PSG noise control resets shift register", PsgNoiseControlResetsShiftRegister);
Run("YM2612 timers and status", Ym2612TimersAndStatus);
Run("YM2612 timer reloads from latch writes", Ym2612TimerReloadsFromLatchWrites);
Run("YM2612 busy status after register writes", Ym2612BusyStatusAfterRegisterWrites);
Run("YM2612 DAC reset level is neutral", Ym2612DacResetLevelIsNeutral);
Run("YM2612 CSM key-on from Timer A overflow", Ym2612CsmKeyOnFromTimerAOverflow);
Run("YM2612 DAC writes render at frame timestamps", Ym2612DacWritesRenderAtFrameTimestamps);
Run("YM2612 key-on writes render at frame timestamps", Ym2612KeyOnWritesRenderAtFrameTimestamps);
Run("YM2612 frame events are sorted by timestamp", Ym2612FrameEventsAreSortedByTimestamp);
Run("YM2612 channel stems isolate keyed channel", Ym2612ChannelStemsIsolateKeyedChannel);
Run("YM2612 stereo panning", Ym2612StereoPanning);
Run("YM2612 renders upper-bank keyed channels", Ym2612RendersUpperBankKeyedChannels);
Run("YM2612 algorithm 0 uses S4 carrier", Ym2612Algorithm0UsesS4Carrier);
Run("YM2612 algorithm 4 uses S2 and S4 carriers", Ym2612Algorithm4UsesS2AndS4Carriers);
Run("YM2612 algorithm 5 uses S2 S3 and S4 carriers", Ym2612Algorithm5UsesS2S3AndS4Carriers);
Run("YM2612 selective key-on maps S2 and S3 bits", Ym2612SelectiveKeyOnMapsS2AndS3Bits);
Run("YM2612 snapshots expose feedback and modulation sensitivity", Ym2612SnapshotsExposeFeedbackAndModulationSensitivity);
Run("YM2612 frequency high byte latches on low byte write", Ym2612FrequencyHighByteLatchesOnLowByteWrite);
Run("YM2612 channel 3 special mode affects operator pitch", Ym2612Channel3SpecialModeAffectsOperatorPitch);
Run("YM2612 channel 3 special mode maps operator frequency registers", Ym2612Channel3SpecialModeMapsOperatorFrequencyRegisters);
Run("YM2612 detune affects operator pitch", Ym2612DetuneAffectsOperatorPitch);
Run("YM2612 detune is applied before multiplier", Ym2612DetuneIsAppliedBeforeMultiplier);
Run("YM2612 attack rate zero stays silent", Ym2612AttackRateZeroStaysSilent);
Run("YM2612 SSG envelope cycles", Ym2612SsgEnvelopeCycles);
Run("YM2612 total level attenuation can mute carriers", Ym2612TotalLevelAttenuationCanMuteCarriers);
Run("YM2612 sustain level zero remains audible", Ym2612SustainLevelZeroRemainsAudible);
Run("YM2612 sustain level fifteen decays to silence", Ym2612SustainLevelFifteenDecaysToSilence);
Run("YM2612 low sustain rates decay gradually", Ym2612LowSustainRatesDecayGradually);
Run("Z80 core executes basic program", Z80CoreExecutesBasicProgram);
Run("Z80 core executes Sonic bank init loop", Z80CoreExecutesSonicBankInitLoop);
Run("Z80 JR uses displacement after operand", Z80JrUsesDisplacementAfterOperand);
Run("Z80 core executes CB prefix stack and conditional flow", Z80CoreExecutesCbStackAndConditionalFlow);
Run("Z80 core executes register ALU variants", Z80CoreExecutesRegisterAluVariants);
Run("Z80 accumulator flag instructions", Z80AccumulatorFlagInstructions);
Run("Z80 maskable interrupt enters IM1 vector after EI delay", Z80MaskableInterruptEntersIm1VectorAfterEiDelay);
Run("Z80 maskable interrupt supports IM2 vectors", Z80MaskableInterruptSupportsIm2Vectors);
Run("Z80 core executes ED and indexed operations", Z80CoreExecutesEdAndIndexedOperations);
Run("Z80 NEG aliases update flags", Z80NegAliasesUpdateFlags);
Run("Z80 bus writes YM2612", Z80BusWritesYm2612);
Run("Z80 bus exposes banked 68k window", Z80BusExposesBanked68kWindow);
Run("Z80 reset and bus request word writes use even byte", Z80ControlWordWritesUseEvenByte);
Run("Z80 bus grant is delayed after request", Z80BusGrantIsDelayedAfterRequest);
Run("Z80 runs during short 68k bus release windows", Z80RunsDuringShortBusReleaseWindows);
Run("Z80 audio timestamps remain monotonic across frames", Z80AudioTimestampsRemainMonotonicAcrossFrames);
Run("Z80 receives VBlank interrupt pulse", Z80ReceivesVBlankInterruptPulse);
Run("Z80 VBlank interrupt is independent of 68k VBlank enable", Z80VBlankInterruptIgnores68kEnable);
Run("Sonic 1 startup streams YM DAC sample", Sonic1StartupStreamsYmDacSample);
Run("Sonic 1 title drives YM and PSG music", Sonic1TitleDrivesYmAndPsgMusic);
Run("work RAM mirroring", WorkRamMirroring);
Run("cartridge save RAM", CartridgeSaveRam);
Run("cartridge save RAM byte lanes", CartridgeSaveRamByteLanes);
Run("cartridge serial EEPROM", CartridgeSerialEeprom);
Run("input movie preserves initial save RAM", InputMoviePreservesInitialSaveRam);
Run("cartridge bank switching", CartridgeBankSwitching);
Run("J-Cart controller ports", JCartControllerPorts);
Run("SVP cartridge memory map", SvpCartridgeMemoryMap);
Run("SVP immediate ops use reference timing", SvpImmediateOpsUseReferenceTiming);
Run("SVP optional MAME timing charges immediate cycles", SvpOptionalMameTimingChargesImmediateCycles);
Run("SVP MLD clears status flags without setting Z", SvpMldClearsStatusFlagsWithoutSettingZ);
Run("SVP AL read preserves pending PMAC except dummy assign", SvpAlReadPreservesPendingPmacExceptDummyAssign);
Run("SVP pointer writes ignore modulo length", SvpPointerWritesIgnoreModuloLength);
Run("SVP PM trace captures DRAM writes", SvpPmTraceCapturesDramWrites);
Run("SVP pointer trace captures RAM operands", SvpPointerTraceCapturesRamOperands);
Run("save-state round trip", SaveStateRoundTrip);
Run("synthetic Genesis startup ROM", SyntheticGenesisStartupRom);
Run("synthetic Genesis VBlank interrupt", SyntheticGenesisVBlankInterrupt);
Run("synthetic Genesis pending VBlank interrupt after unmask", SyntheticGenesisPendingVBlankInterruptAfterUnmask);
Run("expanded 68k arithmetic and MOVEM", ExpandedCpuInstructions);
Run("68000 MOVE.B DBF fill loop fast-forward", M68kMoveByteDbfFillLoopFastForward);
Run("68000 TST.L BNE wait loop fast-forward", M68kTstLongBneWaitLoopFastForward);
Run("68000 MOVEM predecrement stores original address register", MovemPredecrementStoresOriginalAddressRegister);
Run("68000 multiply instructions", MultiplyInstructions);
Run("68000 EOR and CMPM instructions", EorAndCmpmInstructions);
Run("68000 NEG Scc CHK and MOVEP instructions", NegSccChkAndMovepInstructions);
Run("68000 MOVE from SR instruction", MoveFromSrInstruction);
Run("68000 exchange TAS and dynamic bit ops", ExchangeTasAndBitOps);
Run("68000 immediate bit write ops", ImmediateBitWriteOps);
Run("68000 illegal exception and RTE", IllegalExceptionAndRte);
Run("68000 ILLEGAL opcode vectors without extension word", IllegalOpcodeVectorsWithoutExtensionWord);
Run("68000 invalid MOVEA.B vectors illegal", InvalidMoveaByteVectorsIllegal);
Run("68000 invalid effective address vectors illegal", InvalidEffectiveAddressVectorsIllegal);
Run("68000 MOVE USP directions and privilege", MoveUspDirectionsAndPrivilege);
Run("68000 RTE restores user-mode PC from supervisor stack", RteReturnsToUserModeFromSupervisorStack);
Run("68000 interrupt switches from user stack to supervisor stack", InterruptSwitchesFromUserStackToSupervisorStack);
Run("68000 RTR restores CCR and PC", RtrRestoresCcrAndPc);
Run("68000 RESET is privileged and resumes", ResetInstructionIsPrivilegedAndResumes);
Run("68000 MOVE.W postincrement to VDP absolute long", MovePostincrementToVdpAbsoluteLong);
Run("68000 DBRA displacement word origin", DbraUsesDisplacementWordOrigin);
Run("68000 BRA.W displacement word origin", BraWordUsesDisplacementWordOrigin);
Run("68000 program counter uses 24-bit address bus", ProgramCounterUses24BitAddressBus);
Run("68000 PC-relative EA uses extension word origin", PcRelativeEffectiveAddressUsesExtensionWordOrigin);
Run("68000 immediate RMW absolute long evaluates EA once", ImmediateRmwAbsoluteLongEvaluatesEaOnce);
Run("68000 ASR sign extends byte and word register operands", AsrSignExtendsRegisterOperands);
Run("68000 register shift count zero is no-op", RegisterShiftCountZeroIsNoOp);
Run("68000 register shift counts above operand width", RegisterShiftCountsAboveOperandWidth);
Run("68000 rotate and arithmetic shift flags", RotateAndArithmeticShiftFlags);
Run("68000 ADDX and SUBX instructions", AddSubXInstructions);
Run("68000 BCD arithmetic instructions", BcdArithmeticInstructions);
Run("VDP frame renderer draws plane tiles", VdpFrameRendererDrawsPlaneTiles);
Run("VDP plane pixel trace maps tile source", VdpPlanePixelTraceMapsTileSource);
Run("VDP interlace double mode uses 8x16 tiles", VdpInterlaceDoubleModeUsesTallTiles);
Run("VDP frame renderer draws sprites", VdpFrameRendererDrawsSprites);
Run("VDP H32 sprites use active display coordinates", VdpH32SpritesUseActiveDisplayCoordinates);
Run("VDP interlace sprites use source coordinates", VdpInterlaceSpritesUseSourceCoordinates);
Run("VDP sprite Y coordinate ignores high bits", VdpSpriteYCoordinateIgnoresHighBits);
Run("VDP frame renderer uses per-line sprite snapshots", VdpFrameRendererUsesPerLineSpriteSnapshots);
Run("VDP frame renderer uses active-frame VRAM snapshot", VdpFrameRendererUsesActiveFrameVramSnapshot);
Run("VDP frame renderer uses per-line plane VRAM snapshots", VdpFrameRendererUsesPerLinePlaneVramSnapshots);
Run("VDP DMA timing snapshots preserve partial VRAM", VdpDmaTimingSnapshotsPreservePartialVram);
Run("VDP frame renderer uses per-line sprite pattern snapshots", VdpFrameRendererUsesPerLineSpritePatternSnapshots);
Run("VDP frame renderer draws multi-cell sprites in VDP order", VdpFrameRendererDrawsMultiCellSpritesInVdpOrder);
Run("VDP frame renderer blanks when display is disabled", VdpFrameRendererBlanksWhenDisplayIsDisabled);
Run("VDP frame renderer applies scroll", VdpFrameRendererAppliesScroll);
Run("VDP interlace double mode indexes hscroll by display line", VdpInterlaceDoubleModeIndexesHscrollByDisplayLine);
Run("VDP frame renderer uses per-line H-scroll snapshots", VdpFrameRendererUsesPerLineHscrollSnapshots);
Run("VDP frame renderer uses per-line register snapshots", VdpFrameRendererUsesPerLineRegisterSnapshots);
Run("VDP frame renderer uses per-line CRAM snapshots", VdpFrameRendererUsesPerLineCramSnapshots);
Run("VDP frame renderer uses per-line VSRAM snapshots", VdpFrameRendererUsesPerLineVsramSnapshots);
Run("VDP frame renderer applies window plane", VdpFrameRendererAppliesWindowPlane);
Run("VDP H40 window uses 64-cell row stride", VdpH40WindowUsesSixtyFourCellStride);
Run("VDP frame renderer applies priority", VdpFrameRendererAppliesPriority);
Run("VDP sprite link priority feeds layer priority", VdpSpriteLinkPriorityFeedsLayerPriority);
Run("VDP frame renderer applies shadow highlight", VdpFrameRendererAppliesShadowHighlight);
Run("VDP sprite mask preserves high priority sprites", VdpSpriteMaskPreservesHighPrioritySprites);
Run("VDP sprite dot limit clips the final sprite", VdpSpriteDotLimitClipsFinalSprite);
Run("VDP sprite status flags", VdpSpriteStatusFlags);
Run("VDP direct color DMA capture renders frame", VdpDirectColorDmaCaptureRendersFrame);

if (failures > 0)
{
    Console.Error.WriteLine($"{failures} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine("All tests passed.");

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

void CartridgeHeaderParsing()
{
    byte[] rom = CreateRom();
    WriteAscii(rom, 0x100, "SEGA MEGA DRIVE ");
    WriteAscii(rom, 0x120, "DOMESTIC");
    WriteAscii(rom, 0x150, "OVERSEAS");
    WriteAscii(rom, 0x180, "GM TEST-00");
    WriteAscii(rom, 0x1F0, "JUE");

    CartridgeImage image = CartridgeImage.FromBytes(rom);
    AssertEqual("SEGA MEGA DRIVE", image.Header.ConsoleName);
    AssertEqual("DOMESTIC", image.Header.DomesticName);
    AssertEqual("OVERSEAS", image.Header.OverseasName);
    AssertEqual("GM TEST-00", image.Header.ProductCode);
    AssertEqual("JUE", image.Header.Region);
    AssertTrue(!image.Header.PrefersPal, "mixed JUE region should default to NTSC timing");

    WriteAscii(rom, 0x1F0, "8");
    image = CartridgeImage.FromBytes(rom);
    AssertTrue(image.Header.PrefersPal, "numeric 8 region code should select PAL timing");
}

void CartridgeDiagnosticsReport()
{
    byte[] sramRom = CreateRom();
    WriteLong(sramRom, 0x1A0, 0x0000_0000);
    WriteLong(sramRom, 0x1A4, (uint)(sramRom.Length - 1));
    DeclareSaveRam(sramRom, 0x0020_0001, 0x0020_3FFF, 0x20);

    CartridgeDiagnostics sram = CartridgeImage.FromBytes(sramRom).Diagnostics;
    AssertEqual("SRAM", sram.SaveHardware);
    AssertEqual("odd", sram.SaveRamLanes);
    AssertTrue(sram.SaveRamStart == 0x0020_0001, "SRAM start should be reported");
    AssertTrue(sram.SaveRamEnd == 0x0020_3FFF, "SRAM end should be reported");
    AssertTrue(!sram.HasUnsupportedHardware, "plain SRAM cartridge should not report unsupported hardware");

    byte[] largeRom = new byte[0x50_0000];
    WriteLong(largeRom, 0x1A0, 0x0000_0000);
    WriteLong(largeRom, 0x1A4, (uint)(largeRom.Length - 1));
    AssertTrue(CartridgeImage.FromBytes(largeRom).Diagnostics.UsesBankSwitchRegisters, "large cartridges should report expected bank switching");

    byte[] svpRom = CreateRom();
    WriteAscii(svpRom, 0x150, "VIRTUA RACING");
    CartridgeDiagnostics svp = CartridgeImage.FromBytes(svpRom).Diagnostics;
    AssertTrue(!svp.HasUnsupportedHardware, "SVP cartridges should be supported by the cartridge mapper");
    AssertTrue(svp.HasSvp, "SVP diagnostic should flag the coprocessor");
    AssertTrue(svp.Warnings.Any(item => item.Contains("SVP", StringComparison.Ordinal)), "SVP diagnostic should name the coprocessor");

    byte[] x32Rom = CreateRom();
    WriteAscii(x32Rom, 0x100, "SEGA 32X");
    CartridgeDiagnostics x32 = CartridgeImage.FromBytes(x32Rom).Diagnostics;
    AssertTrue(x32.Requires32X, "32X diagnostic should expose a first-class 32X requirement flag");
    AssertTrue(x32.UnsupportedHardware.Any(item => item.Contains("32X", StringComparison.Ordinal)), "32X diagnostic should name unsupported 32X hardware");

    byte[] jCartRom = CreateRom();
    WriteAscii(jCartRom, 0x150, "MICRO MACHINES II");
    CartridgeDiagnostics jCart = CartridgeImage.FromBytes(jCartRom).Diagnostics;
    AssertTrue(!jCart.HasUnsupportedHardware, "J-Cart games should still boot with normal controller ports");
    AssertTrue(jCart.Warnings.Any(item => item.Contains("J-Cart", StringComparison.Ordinal)), "J-Cart diagnostic should call out extra controller ports");

    byte[] t2ActionRom = CreateRom();
    WriteAscii(t2ActionRom, 0x150, "TERMINATOR 2");
    CartridgeDiagnostics t2Action = CartridgeImage.FromBytes(t2ActionRom).Diagnostics;
    AssertTrue(!t2Action.Warnings.Any(item => item.Contains("Light gun", StringComparison.Ordinal)), "non-arcade Terminator 2 should not be flagged as a light gun game");

    byte[] t2ArcadeRom = CreateRom();
    WriteAscii(t2ArcadeRom, 0x150, "T2 THE ARCADE GAME");
    CartridgeDiagnostics t2Arcade = CartridgeImage.FromBytes(t2ArcadeRom).Diagnostics;
    AssertTrue(t2Arcade.Warnings.Any(item => item.Contains("Light gun", StringComparison.Ordinal)), "T2 arcade should be flagged as a light gun game");
}

void ThirtyTwoXHardwareProfileReport()
{
    AssertEqual(2, ThirtyTwoXHardwareProfile.Sh2CpuCount);
    AssertEqual(256 * 1024, ThirtyTwoXHardwareProfile.SdramBytes);
    AssertEqual(128 * 1024, ThirtyTwoXHardwareProfile.FrameBufferBytes);
    AssertEqual(0xA1_5100u, ThirtyTwoXHardwareProfile.M68kSystemRegisterStart);
    AssertEqual(0xA1_5130u, ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.PwmControlOffset));
    AssertEqual(0xA1_5180u, ThirtyTwoXHardwareProfile.M68kVdpRegisterStart);
    AssertEqual(0x2000_4030u, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.PwmControlOffset));
    AssertEqual(0x0000_0000u, ThirtyTwoXHardwareProfile.Sh2CartridgeLowCachedStart);
    AssertEqual(0x0200_0000u, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedCachedStart);
    AssertEqual(0x2200_0000u, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    AssertTrue(ThirtyTwoXHardwareProfile.RequiredSubsystems.Length >= 6, "32X plan should name the major subsystems");
}

void ThirtyTwoXDeviceShell()
{
    ThirtyTwoXDevice device = new();
    device.Reset();
    static void LatchThirtyTwoXVdp(ThirtyTwoXDevice target)
    {
        target.SetHBlank(true);
        target.SetHBlank(false);
    }

    static byte ReadSh2ByteForTest(ThirtyTwoXDevice target, uint address)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Byte helper was not found");
        return (byte)method.Invoke(target, [address, 0])!;
    }

    static ushort ReadSh2WordForTest(ThirtyTwoXDevice target, uint address)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Word", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Word helper was not found");
        return (ushort)method.Invoke(target, [address, 0])!;
    }

    static void WriteSh2ByteForTest(ThirtyTwoXDevice target, uint address, byte value, int cpuIndex = 0)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Byte helper was not found");
        method.Invoke(target, [address, value, cpuIndex]);
    }

    static void WriteSh2WordForTest(ThirtyTwoXDevice target, uint address, ushort value, int cpuIndex = 0)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Word", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Word helper was not found");
        method.Invoke(target, [address, value, cpuIndex]);
    }

    static void WriteSh2LongForTest(ThirtyTwoXDevice target, uint address, uint value, int cpuIndex = 0)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Long", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Long helper was not found");
        method.Invoke(target, [address, value, cpuIndex]);
    }

    AssertTrue(device.Sh2HeldInReset, "32X shell should hold SH-2s until adapter control releases them");
    AssertEqual((ushort)0x8000, device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset));
    device.WriteVdpRegisterWord(0x0072, 0xFFFF);
    AssertEqual((ushort)0x0000, device.ReadVdpRegisterWord(0x0072));
    AssertEqual((byte)0x00, ReadSh2ByteForTest(device, ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart + 0x72));
    device.WriteFrameBufferByte(0, 0x5A);
    device.WriteFrameBufferByte(0, 0x00);
    AssertEqual((byte)0x00, device.ReadFrameBufferByte(0));
    device.WriteFrameBufferByte(0, 0x5A);
    device.WriteOverwriteImageByte(0, 0x00);
    AssertEqual((byte)0x5A, device.ReadFrameBufferByte(0));
    List<ThirtyTwoXDevice.PaletteAccessTrace> paletteEvents = [];
    device.PaletteAccessObserver = paletteEvents.Add;
    device.WritePaletteWord(0x1FE, 0x2468, "TEST");
    AssertEqual((ushort)0x2468, device.ReadPaletteWord(0x1FE));
    AssertEqual(2, paletteEvents.Count);
    AssertEqual("TEST", paletteEvents[0].Source);
    AssertEqual("W16", paletteEvents[0].Operation);
    AssertEqual((ushort)0x1FE, paletteEvents[0].Offset);
    AssertEqual((ushort)0x2468, paletteEvents[0].Value);
    AssertEqual("R16", paletteEvents[1].Operation);
    ThirtyTwoXDevice ntscFormatDevice = new();
    ntscFormatDevice.Reset();
    ntscFormatDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);
    AssertEqual((ushort)0x8001, ntscFormatDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset));
    ThirtyTwoXDevice palDevice = new(pal: true);
    palDevice.Reset();
    AssertEqual((ushort)0x0000, palDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset));
    palDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x8001);
    AssertEqual((ushort)0x0001, palDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset));
    ThirtyTwoXDevice statusDevice = new();
    statusDevice.Reset();
    AssertEqual((ushort)0x2000, statusDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset));
    statusDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);
    AssertEqual((ushort)0x0002, statusDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset));
    statusDevice.SetHBlank(true);
    AssertEqual((ushort)0x4000, statusDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset));
    statusDevice.SetHBlank(false);
    statusDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0002);
    AssertEqual((ushort)0x2002, statusDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset));
    statusDevice.StepScanline(224, pal: false);
    AssertEqual((ushort)0xA000, statusDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset));
    statusDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.BankSetOffset, 0x0002);
    statusDevice.StepScanline(37, pal: false);
    AssertEqual((ushort)0x0002, statusDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.BankSetOffset));
    AssertEqual((ushort)0x0025, ReadSh2WordForTest(statusDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.HCountOffset)));
    AssertEqual((byte)0x25, ReadSh2ByteForTest(statusDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.HCountOffset) + 1));
    ThirtyTwoXDevice restoredHCountDevice = new();
    restoredHCountDevice.RestoreState(statusDevice.CaptureState());
    AssertEqual((ushort)0x0025, ReadSh2WordForTest(restoredHCountDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.HCountOffset)));
    ThirtyTwoXDevice interruptClearDevice = new();
    interruptClearDevice.Reset();
    interruptClearDevice.RestoreState(interruptClearDevice.CaptureState() with
    {
        MasterVerticalInterruptPending = true,
        SlaveVerticalInterruptPending = true,
        MasterVresInterruptPending = true,
        SlaveVresInterruptPending = true,
        MasterHorizontalInterruptPending = true,
        SlaveHorizontalInterruptPending = true,
        MasterPwmInterruptPending = true,
        SlavePwmInterruptPending = true,
    });
    WriteSh2WordForTest(interruptClearDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.VResInterruptClearOffset), 0, cpuIndex: 0);
    WriteSh2WordForTest(interruptClearDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.VInterruptClearOffset), 0, cpuIndex: 0);
    WriteSh2WordForTest(interruptClearDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.HInterruptClearOffset), 0, cpuIndex: 0);
    WriteSh2WordForTest(interruptClearDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.PwmInterruptClearOffset), 0, cpuIndex: 0);
    ThirtyTwoXDevice.ThirtyTwoXState clearedState = interruptClearDevice.CaptureState();
    AssertTrue(!clearedState.MasterVresInterruptPending, "master VRES interrupt clear should clear master latch");
    AssertTrue(clearedState.SlaveVresInterruptPending, "master VRES interrupt clear should not clear slave latch");
    AssertTrue(!clearedState.MasterVerticalInterruptPending, "master V interrupt clear should clear master latch");
    AssertTrue(clearedState.SlaveVerticalInterruptPending, "master V interrupt clear should not clear slave latch");
    AssertTrue(!clearedState.MasterHorizontalInterruptPending, "master H interrupt clear should clear master latch");
    AssertTrue(clearedState.SlaveHorizontalInterruptPending, "master H interrupt clear should not clear slave latch");
    AssertTrue(!clearedState.MasterPwmInterruptPending, "master PWM interrupt clear should clear master latch");
    AssertTrue(clearedState.SlavePwmInterruptPending, "master PWM interrupt clear should not clear slave latch");
    ThirtyTwoXDevice vresDevice = new();
    vresDevice.Reset();
    vresDevice.TriggerResetButtonInterrupt();
    AssertEqual(0, vresDevice.MasterSh2.PendingInterruptLevel);
    vresDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    vresDevice.TriggerResetButtonInterrupt();
    AssertEqual(14, vresDevice.MasterSh2.PendingInterruptLevel);
    AssertEqual(71, vresDevice.MasterSh2.PendingInterruptVectorNumber);
    AssertEqual(14, vresDevice.SlaveSh2.PendingInterruptLevel);
    ThirtyTwoXDevice restoredVresDevice = new();
    restoredVresDevice.RestoreState(vresDevice.CaptureState());
    AssertTrue(restoredVresDevice.CaptureState().MasterVresInterruptPending, "VRES latch should survive capture/restore");
    WriteSh2ByteForTest(restoredVresDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.VResInterruptClearOffset) + 1, 0);
    AssertEqual(0, restoredVresDevice.MasterSh2.PendingInterruptLevel);
    AssertTrue(!restoredVresDevice.CaptureState().MasterVresInterruptPending, "byte clear should clear master VRES latch");
    AssertTrue(restoredVresDevice.CaptureState().SlaveVresInterruptPending, "byte clear should preserve slave VRES latch");
    ThirtyTwoXDevice commandClearDevice = new();
    commandClearDevice.Reset();
    commandClearDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset, 0x0003);
    commandClearDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset, 0x0000);
    AssertEqual((ushort)0x0003, commandClearDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset));
    WriteSh2WordForTest(commandClearDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommandInterruptClearOffset), 0, cpuIndex: 0);
    ThirtyTwoXDevice.ThirtyTwoXState commandClearState = commandClearDevice.CaptureState();
    AssertTrue(!commandClearState.MasterCommandInterruptPending, "master CMD interrupt clear should clear master latch");
    AssertTrue(commandClearState.SlaveCommandInterruptPending, "master CMD interrupt clear should not clear slave latch");
    AssertEqual((ushort)0x0002, commandClearDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset));
    commandClearDevice.WriteSystemRegisterByte((ushort)(ThirtyTwoXHardwareProfile.InterruptControlOffset + 1), 0x01);
    AssertEqual((ushort)0x0003, commandClearDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset));
    WriteSh2WordForTest(commandClearDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommandInterruptClearOffset), 0, cpuIndex: 1);
    AssertEqual((ushort)0x0001, commandClearDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset));
    WriteSh2ByteForTest(commandClearDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommandInterruptClearOffset) + 1, 0);
    AssertEqual((ushort)0x0000, commandClearDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset));
    ThirtyTwoXDevice maskedCommandDevice = new();
    maskedCommandDevice.Reset();
    maskedCommandDevice.WriteSystemRegisterByte((ushort)(ThirtyTwoXHardwareProfile.InterruptControlOffset + 1), 0x01);
    AssertEqual(0, maskedCommandDevice.MasterSh2.PendingInterruptLevel);
    WriteSh2WordForTest(maskedCommandDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.AdapterControlOffset), 0x0002);
    AssertEqual(8, maskedCommandDevice.MasterSh2.PendingInterruptLevel);
    AssertEqual(68, maskedCommandDevice.MasterSh2.PendingInterruptVectorNumber);
    ThirtyTwoXDevice byteMaskAccessDevice = new();
    byteMaskAccessDevice.Reset();
    WriteSh2ByteForTest(byteMaskAccessDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.AdapterControlOffset), 0x80);
    WriteSh2ByteForTest(byteMaskAccessDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.AdapterControlOffset) + 1, 0x08);
    AssertTrue(byteMaskAccessDevice.VdpAccessGrantedToSh2, "low-byte SH-2 interrupt mask writes should preserve VDP ownership granted by the high byte");
    AssertEqual((ushort)0x8008, ReadSh2WordForTest(byteMaskAccessDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.AdapterControlOffset)));
    ThirtyTwoXDevice byteCommandDevice = new();
    byteCommandDevice.Reset();
    byteCommandDevice.WriteSystemRegisterByte((ushort)(ThirtyTwoXHardwareProfile.InterruptControlOffset + 1), 0x02);
    byteCommandDevice.WriteSystemRegisterByte((ushort)(ThirtyTwoXHardwareProfile.InterruptControlOffset + 1), 0x00);
    AssertEqual((ushort)0x0002, byteCommandDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset));
    ThirtyTwoXDevice staleCommDevice = new();
    staleCommDevice.Reset();
    staleCommDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8), 0x50D4);
    WriteSh2WordForTest(staleCommDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8), 0x0054, cpuIndex: 1);
    AssertEqual((ushort)0x50D4, staleCommDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8)));
    AssertEqual((ushort)0x0054, staleCommDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8)));
    ThirtyTwoXDevice postBootTokenClearDevice = new();
    postBootTokenClearDevice.Reset();
    WriteSh2WordForTest(postBootTokenClearDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8), 0x534C, cpuIndex: 1);
    WriteSh2WordForTest(postBootTokenClearDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8), 0x0000, cpuIndex: 0);
    AssertEqual((ushort)0x534C, postBootTokenClearDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8)));
    AssertEqual((ushort)0x0000, postBootTokenClearDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8)));
    ThirtyTwoXDevice peerReadyProbeDevice = new();
    peerReadyProbeDevice.Reset();
    ThirtyTwoXDevice.ThirtyTwoXState peerReadyProbeState = peerReadyProbeDevice.CaptureState();
    byte[] peerReadyProbeRegisters = (byte[])peerReadyProbeState.SystemRegisters.Clone();
    peerReadyProbeRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2] = 0x4F;
    peerReadyProbeRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + 3] = 0x4B;
    peerReadyProbeDevice.RestoreState(peerReadyProbeState with
    {
        SystemRegisters = peerReadyProbeRegisters,
        BootRomHandshakePending = false,
        BootRomPostStartSignaturePending = false,
        BootRomPostStartSignatureHiddenFromSh2 = true,
    });
    WriteSh2WordForTest(
        peerReadyProbeDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2),
        0xBEEF);
    AssertEqual((ushort)0xDEAF, ReadSh2WordForTest(peerReadyProbeDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2)));
    ThirtyTwoXDevice lowerCommandClearDevice = new();
    lowerCommandClearDevice.Reset();
    lowerCommandClearDevice.WriteSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset, 0xFF);
    WriteSh2ByteForTest(lowerCommandClearDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset), 0, cpuIndex: 0);
    AssertEqual((byte)0x00, lowerCommandClearDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset));
    ThirtyTwoXDevice upperCommandClearDevice = new();
    upperCommandClearDevice.Reset();
    upperCommandClearDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12), 0xFFFF);
    WriteSh2WordForTest(upperCommandClearDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12), 0, cpuIndex: 0);
    AssertEqual((ushort)0x0000, upperCommandClearDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12)));
    ThirtyTwoXDevice sixtyEightUpDevice = new();
    sixtyEightUpDevice.Reset();
    sixtyEightUpDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12), 0x3638);
    sixtyEightUpDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14), 0x5550);
    AssertEqual((ushort)0x5550, sixtyEightUpDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14)));
    AssertEqual((ushort)0x0000, ReadSh2WordForTest(sixtyEightUpDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14)));
    sixtyEightUpDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12), 0x0000);
    sixtyEightUpDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14), 0x0000);
    AssertEqual((ushort)0x475F, sixtyEightUpDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12)));
    AssertEqual((ushort)0x4F4B, sixtyEightUpDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14)));
    AssertEqual((ushort)0x475F, ReadSh2WordForTest(sixtyEightUpDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12)));
    AssertEqual((ushort)0x0000, ReadSh2WordForTest(sixtyEightUpDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14)));
    AssertEqual(0x475F_4F4Bu, ReadSh2LongForTest(sixtyEightUpDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12)));
    AssertEqual((ushort)0x0000, sixtyEightUpDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12)));
    AssertEqual((ushort)0x0000, sixtyEightUpDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14)));
    sixtyEightUpDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14), 0x0000);
    AssertEqual((ushort)0x0000, ReadSh2WordForTest(sixtyEightUpDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14)));
    ThirtyTwoXDevice directGOkDevice = new();
    directGOkDevice.Reset();
    directGOkDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12), 0x475F);
    directGOkDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14), 0x4F4B);
    WriteSh2LongForTest(directGOkDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12), 0);
    AssertEqual((ushort)0x0000, directGOkDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12)));
    AssertEqual((ushort)0x0000, directGOkDevice.ReadSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14)));
    ThirtyTwoXDevice dualWorkerSemaphoreDevice = new();
    dualWorkerSemaphoreDevice.Reset();
    const uint dualWorkerWrapperAddress = ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x100;
    dualWorkerSemaphoreDevice.MasterSh2.R[13] = dualWorkerWrapperAddress;
    WriteSh2WordForTest(dualWorkerSemaphoreDevice, dualWorkerWrapperAddress + 0x00, 0xDA05);
    WriteSh2WordForTest(dualWorkerSemaphoreDevice, dualWorkerWrapperAddress + 0x02, 0xDB04);
    WriteSh2WordForTest(dualWorkerSemaphoreDevice, dualWorkerWrapperAddress + 0x04, 0xD002);
    WriteSh2WordForTest(dualWorkerSemaphoreDevice, dualWorkerWrapperAddress + 0x06, 0x4F22);
    WriteSh2WordForTest(dualWorkerSemaphoreDevice, dualWorkerWrapperAddress + 0x08, 0x400B);
    WriteSh2WordForTest(dualWorkerSemaphoreDevice, dualWorkerWrapperAddress + 0x0A, 0x0009);
    WriteSh2WordForTest(dualWorkerSemaphoreDevice, dualWorkerWrapperAddress + 0x0C, 0x000B);
    WriteSh2WordForTest(dualWorkerSemaphoreDevice, dualWorkerWrapperAddress + 0x0E, 0x4F26);
    WriteSh2LongForTest(dualWorkerSemaphoreDevice, dualWorkerWrapperAddress + 0x10, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x4C28);
    WriteSh2LongForTest(dualWorkerSemaphoreDevice, dualWorkerWrapperAddress + 0x14, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x4A98);
    WriteSh2LongForTest(dualWorkerSemaphoreDevice, dualWorkerWrapperAddress + 0x18, 0x0000_0002);
    dualWorkerSemaphoreDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8, 0x0600);
    dualWorkerSemaphoreDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 10, 0x0200);
    dualWorkerSemaphoreDevice.WriteSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset, 0xFF);
    WriteSh2ByteForTest(dualWorkerSemaphoreDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset), 0, cpuIndex: 0);
    AssertEqual((byte)0x00, dualWorkerSemaphoreDevice.Sdram[0x200]);
    AssertEqual((byte)0x02, dualWorkerSemaphoreDevice.Sdram[0x201]);
    AssertEqual((byte)0x00, dualWorkerSemaphoreDevice.Sdram[0x202]);
    AssertEqual((byte)0x01, dualWorkerSemaphoreDevice.Sdram[0x203]);
    ThirtyTwoXDevice dualWorkerMismatchDevice = new();
    dualWorkerMismatchDevice.Reset();
    dualWorkerMismatchDevice.MasterSh2.R[13] = dualWorkerWrapperAddress;
    WriteSh2WordForTest(dualWorkerMismatchDevice, dualWorkerWrapperAddress + 0x00, 0xDA05);
    WriteSh2WordForTest(dualWorkerMismatchDevice, dualWorkerWrapperAddress + 0x02, 0x0009);
    WriteSh2LongForTest(dualWorkerMismatchDevice, dualWorkerWrapperAddress + 0x10, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x4C28);
    WriteSh2LongForTest(dualWorkerMismatchDevice, dualWorkerWrapperAddress + 0x14, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x4A98);
    WriteSh2LongForTest(dualWorkerMismatchDevice, dualWorkerWrapperAddress + 0x18, 0x0000_0002);
    dualWorkerMismatchDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8, 0x0600);
    dualWorkerMismatchDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 10, 0x0200);
    dualWorkerMismatchDevice.WriteSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset, 0xFF);
    WriteSh2ByteForTest(dualWorkerMismatchDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset), 0, cpuIndex: 0);
    AssertEqual((byte)0x00, dualWorkerMismatchDevice.Sdram[0x200]);
    AssertEqual((byte)0x00, dualWorkerMismatchDevice.Sdram[0x201]);
    AssertEqual((byte)0x00, dualWorkerMismatchDevice.Sdram[0x202]);
    AssertEqual((byte)0x00, dualWorkerMismatchDevice.Sdram[0x203]);
    ThirtyTwoXDevice dmaVectorDevice = new();
    dmaVectorDevice.Reset();
    const uint sh2DmaRegisterStart = 0xFFFF_FF80;
    const uint sh2IpraDmaPriorityAddress = 0xFFFF_FEE2;
    WriteSh2ByteForTest(dmaVectorDevice, sh2IpraDmaPriorityAddress, 0x04, cpuIndex: 1);
    WriteSh2LongForTest(dmaVectorDevice, sh2DmaRegisterStart + 0x28, 0x0000_0042, cpuIndex: 1);
    WriteSh2LongForTest(dmaVectorDevice, sh2DmaRegisterStart + 0x34, 0x0000_0001, cpuIndex: 1);
    WriteSh2LongForTest(dmaVectorDevice, sh2DmaRegisterStart + 0x1C, 0x0000_0006, cpuIndex: 1);
    AssertEqual(4, dmaVectorDevice.SlaveSh2.PendingInterruptLevel);
    AssertEqual(66, dmaVectorDevice.SlaveSh2.PendingInterruptVectorNumber);
    AssertEqual(0, device.RunSh2(4));

    byte[] cycleRom = new byte[0x20];
    WriteWord(cycleRom, 0x00, 0x0009); // NOP, 1 cycle.
    WriteWord(cycleRom, 0x02, 0x000B); // RTS, 2 cycles with delay slot.
    WriteWord(cycleRom, 0x04, 0x0009); // delay slot NOP.
    ThirtyTwoXDevice cycleDevice = new(cycleRom);
    cycleDevice.Reset();
    cycleDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    cycleDevice.RestoreState(cycleDevice.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        VdpAccessGrantedToSh2 = true,
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
    });
    int cycleSteps = cycleDevice.RunSh2Cycles(1);
    AssertEqual(2, cycleSteps);
    AssertEqual(13L, cycleDevice.MasterSh2.Cycles);
    AssertEqual(13L, cycleDevice.SlaveSh2.Cycles);
    cycleSteps = cycleDevice.RunSh2Cycles(2);
    AssertEqual(2, cycleSteps);
    AssertEqual(39L, cycleDevice.MasterSh2.Cycles);
    AssertEqual(39L, cycleDevice.SlaveSh2.Cycles);

    AssertEqual((byte)'M', device.ReadSuper32XIdByte(0xA1_30EC));
    AssertEqual((byte)'A', device.ReadSuper32XIdByte(0xA1_30ED));
    AssertEqual((byte)'R', device.ReadSuper32XIdByte(0xA1_30EE));
    AssertEqual((byte)'S', device.ReadSuper32XIdByte(0xA1_30EF));

    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, 0x1ABC);
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, 0x0123);
    ThirtyTwoXDevice.PwmSnapshot pwm = device.CapturePwm();
    AssertEqual(2, pwm.Left.Length);
    AssertEqual((ushort)0x0ABC, pwm.Left[0]);
    AssertEqual((ushort)0x0123, pwm.Left[1]);

    ThirtyTwoXDevice stereoPwmDevice = new();
    stereoPwmDevice.Reset();
    stereoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmCycleOffset, 0x0800);
    stereoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmControlOffset, 0x0009);
    stereoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, 0x0700);
    stereoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmRightPulseWidthOffset, 0x0100);
    stereoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset, 0x0600);
    short[] pwmSamples = new short[8 * 2];
    stereoPwmDevice.RenderPwmStereoSamplesInto(pwmSamples, 8);
    AssertTrue(pwmSamples.Any(sample => sample != 0), "32X PWM writes should render audible samples");
    AssertTrue(pwmSamples[14] > pwmSamples[15], "separate left/right PWM queues should preserve stereo balance");
    ThirtyTwoXDevice routedPwmDevice = new();
    routedPwmDevice.Reset();
    routedPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmCycleOffset, 0x0800);
    routedPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmControlOffset, 0x0006);
    routedPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, 0x0700);
    routedPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmRightPulseWidthOffset, 0x0100);
    short[] routedPwmSamples = new short[8 * 2];
    routedPwmDevice.RenderPwmStereoSamplesInto(routedPwmSamples, 8);
    AssertTrue(routedPwmSamples[15] > routedPwmSamples[14], "PWM routing should allow left/right channels to be swapped");
    ThirtyTwoXDevice fifoPwmDevice = new();
    fifoPwmDevice.Reset();
    fifoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, 0x0100);
    AssertTrue((fifoPwmDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset) & 0x4000) == 0, "non-empty PWM FIFO should clear the empty bit");
    fifoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, 0x0200);
    fifoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, 0x0300);
    AssertTrue((fifoPwmDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset) & 0x8000) != 0, "third PWM queued sample should set the full bit");
    fifoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, 0x0400);
    AssertEqual(3, fifoPwmDevice.CapturePwm().Left.Length);
    AssertEqual((ushort)0x0100, (ushort)(fifoPwmDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset) & 0x0FFF));

    ThirtyTwoXDevice monoPwmDevice = new();
    monoPwmDevice.Reset();
    monoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmCycleOffset, 0x0800);
    monoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset, 0x0555);
    AssertTrue((monoPwmDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset) & 0x4000) == 0, "mono PWM writes should feed the left hardware FIFO");
    AssertTrue((monoPwmDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmRightPulseWidthOffset) & 0x4000) == 0, "mono PWM writes should feed the right hardware FIFO");
    AssertTrue((monoPwmDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset) & 0x4000) == 0, "mono PWM status should derive non-empty state from stereo hardware FIFOs");
    monoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset, 0x0444);
    monoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset, 0x0333);
    AssertTrue((monoPwmDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset) & 0x8000) != 0, "mono PWM status should report full when either stereo hardware FIFO is full");
    monoPwmDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset, 0x0222);
    AssertEqual(3, monoPwmDevice.CapturePwm().Mono.Length);
    AssertEqual((ushort)0x0333, (ushort)(monoPwmDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset) & 0x0FFF));

    AssertEqual((ushort)0x0000, device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x4000, ReadSh2WordForTest(device, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset, 0x0006);
    AssertEqual((ushort)0x0006, device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x4006, ReadSh2WordForTest(device, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset, 0x0007);
    AssertEqual((ushort)0x0004, device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset));
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0x1357);
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0x2468);
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0xAAAA);
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0xBBBB);
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0xCCCC);
    AssertEqual((ushort)0x0002, device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x0000, device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset));
    AssertEqual((ushort)0x1357, device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    AssertEqual((ushort)0x2468, device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    AssertEqual((ushort)0xAAAA, device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    AssertEqual((ushort)0xBBBB, device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    AssertEqual((ushort)0x0002, device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x4002, ReadSh2WordForTest(device, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));

    ThirtyTwoXDevice flushedDreqDevice = new();
    flushedDreqDevice.Reset();
    flushedDreqDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset, 0x0004);
    flushedDreqDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0x1357);
    flushedDreqDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0x2468);
    AssertEqual(2, flushedDreqDevice.DreqFifoCount);
    flushedDreqDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset, 0x0000);
    AssertEqual(0, flushedDreqDevice.DreqFifoCount);
    AssertEqual((ushort)0xFFFF, flushedDreqDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    AssertEqual((ushort)0x4000, ReadSh2WordForTest(flushedDreqDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));

    ThirtyTwoXDevice fullDreqDevice = new();
    fullDreqDevice.Reset();
    fullDreqDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset, 0x0004);
    fullDreqDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset, 0x0008);
    for (ushort i = 0; i < 8; i++)
    {
        fullDreqDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, (ushort)(0x1100 + i));
    }

    fullDreqDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0x22FF);
    AssertEqual((ushort)0x0080, fullDreqDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x8000, ReadSh2WordForTest(fullDreqDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));
    for (ushort i = 0; i < 8; i++)
    {
        AssertEqual((ushort)(0x1100 + i), fullDreqDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    }

    AssertEqual((ushort)0x0000, fullDreqDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x4000, ReadSh2WordForTest(fullDreqDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));

    ThirtyTwoXDevice dreqSnoopDevice = new();
    dreqSnoopDevice.Reset();
    dreqSnoopDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqSourceAddressOffset, 0x0000);
    dreqSnoopDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.DreqSourceAddressOffset + 2), 0x0200);
    dreqSnoopDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset, 0x0004);
    dreqSnoopDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset, 0x0001);
    AssertTrue(!dreqSnoopDevice.SnoopM68kVdpDmaWord(0x0000_0202, 0x9999), "DREQ snoop should wait for the programmed DMA source");
    AssertTrue(dreqSnoopDevice.SnoopM68kVdpDmaWord(0x0000_0200, 0x1111), "DREQ snoop should accept the programmed DMA source");
    AssertTrue(dreqSnoopDevice.SnoopM68kVdpDmaWord(0x0000_0202, 0x2222), "DREQ snoop should advance the programmed source");
    AssertTrue(dreqSnoopDevice.SnoopM68kVdpDmaWord(0x0000_0204, 0x3333), "DREQ snoop should keep accepting sequential DMA words");
    AssertTrue(dreqSnoopDevice.SnoopM68kVdpDmaWord(0x0000_0206, 0x4444), "DREQ snoop should accept the final programmed word");
    AssertEqual((ushort)0x0000, dreqSnoopDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x0000, dreqSnoopDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset));
    AssertEqual((ushort)0x1111, dreqSnoopDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    AssertEqual((ushort)0x2222, dreqSnoopDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    AssertEqual((ushort)0x3333, dreqSnoopDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    AssertEqual((ushort)0x4444, dreqSnoopDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    AssertEqual((ushort)0x0000, dreqSnoopDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x4000, ReadSh2WordForTest(dreqSnoopDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));

    byte[] dreqWordReadRom = new byte[0x40];
    WriteWord(dreqWordReadRom, 0x00, 0xD003); // MOV.L @(literal,PC),R0
    WriteWord(dreqWordReadRom, 0x02, 0x6101); // MOV.W @R0,R1
    WriteWord(dreqWordReadRom, 0x04, 0x6201); // MOV.W @R0,R2
    WriteWord(dreqWordReadRom, 0x06, 0x001B); // SLEEP
    WriteLong(dreqWordReadRom, 0x10, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    ThirtyTwoXDevice dreqWordReadDevice = new(dreqWordReadRom);
    dreqWordReadDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset, 0x0004);
    dreqWordReadDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset, 0x0002);
    dreqWordReadDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0x1357);
    dreqWordReadDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0x2468);
    dreqWordReadDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    dreqWordReadDevice.MasterSh2.Run(8);
    AssertEqual(0x1357u, dreqWordReadDevice.MasterSh2.R[1]);
    AssertEqual(0x2468u, dreqWordReadDevice.MasterSh2.R[2]);
    AssertEqual((ushort)0x0004, dreqWordReadDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x4004, ReadSh2WordForTest(dreqWordReadDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));

    byte[] dreqWordWriteRom = new byte[0x40];
    WriteWord(dreqWordWriteRom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(dreqWordWriteRom, 0x02, 0xD105); // MOV.L @(literal,PC),R1
    WriteWord(dreqWordWriteRom, 0x04, 0x2011); // MOV.W R1,@R0
    WriteWord(dreqWordWriteRom, 0x06, 0x001B); // SLEEP
    WriteLong(dreqWordWriteRom, 0x14, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    WriteLong(dreqWordWriteRom, 0x18, 0x0000_55AA);
    ThirtyTwoXDevice dreqWordWriteDevice = new(dreqWordWriteRom);
    dreqWordWriteDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset, 0x0004);
    dreqWordWriteDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset, 0x0001);
    dreqWordWriteDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    dreqWordWriteDevice.MasterSh2.Run(8);
    AssertEqual(1, dreqWordWriteDevice.DreqFifoWriteCount);
    AssertEqual((ushort)0x55AA, dreqWordWriteDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    AssertEqual((ushort)0x0004, dreqWordWriteDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x4004, ReadSh2WordForTest(dreqWordWriteDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));

    byte[] lowCartridgeAliasRom = new byte[0x80];
    WriteWord(lowCartridgeAliasRom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(lowCartridgeAliasRom, 0x02, 0x6101); // MOV.W @R0,R1
    WriteWord(lowCartridgeAliasRom, 0x04, 0x001B); // SLEEP
    WriteWord(lowCartridgeAliasRom, 0x20, 0x5AA5);
    WriteLong(lowCartridgeAliasRom, 0x14, 0x0000_0020);
    ThirtyTwoXDevice lowCartridgeAliasDevice = new(lowCartridgeAliasRom);
    lowCartridgeAliasDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    lowCartridgeAliasDevice.MasterSh2.Run(8);
    AssertEqual(0x5AA5u, lowCartridgeAliasDevice.MasterSh2.R[1]);
    ThirtyTwoXDevice highOverflowSdramAliasDevice = new();
    highOverflowSdramAliasDevice.Reset();
    WriteSh2WordForTest(highOverflowSdramAliasDevice, 0x592F_D8C2, 0xBEEF);
    AssertEqual((byte)0xBE, highOverflowSdramAliasDevice.Sdram[0x3D8C2]);
    AssertEqual((byte)0xEF, highOverflowSdramAliasDevice.Sdram[0x3D8C3]);
    AssertEqual((ushort)0xBEEF, ReadSh2WordForTest(highOverflowSdramAliasDevice, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x3D8C2));

    byte[] lowCartridgeCacheHitRom = new byte[0x80];
    WriteWord(lowCartridgeCacheHitRom, 0x00, 0xD006); // MOV.L @(literal,PC),R0
    WriteWord(lowCartridgeCacheHitRom, 0x02, 0x6101); // MOV.W @R0,R1, fills line
    WriteWord(lowCartridgeCacheHitRom, 0x04, 0xD106); // MOV.L @(literal,PC),R1
    WriteWord(lowCartridgeCacheHitRom, 0x06, 0x2011); // MOV.W R1,@R0, updates cached line
    WriteWord(lowCartridgeCacheHitRom, 0x08, 0x6201); // MOV.W @R0,R2
    WriteWord(lowCartridgeCacheHitRom, 0x0A, 0x001B); // SLEEP
    WriteWord(lowCartridgeCacheHitRom, 0x30, 0x1111);
    WriteLong(lowCartridgeCacheHitRom, 0x1C, 0x0000_0030);
    WriteLong(lowCartridgeCacheHitRom, 0x20, 0x0000_255A);
    ThirtyTwoXDevice lowCartridgeCacheHitDevice = new(lowCartridgeCacheHitRom);
    lowCartridgeCacheHitDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    WriteSh2ByteForTest(lowCartridgeCacheHitDevice, 0xFFFF_FE92, 0x01);
    lowCartridgeCacheHitDevice.MasterSh2.Run(12);
    AssertEqual(0x255Au, lowCartridgeCacheHitDevice.MasterSh2.R[2]);

    byte[] lowCartridgeCacheWriteAllocateRom = new byte[0x80];
    WriteWord(lowCartridgeCacheWriteAllocateRom, 0x00, 0xD005); // MOV.L @(literal,PC),R0
    WriteWord(lowCartridgeCacheWriteAllocateRom, 0x02, 0xD106); // MOV.L @(literal,PC),R1
    WriteWord(lowCartridgeCacheWriteAllocateRom, 0x04, 0x2011); // MOV.W R1,@R0, misses without allocating a line
    WriteWord(lowCartridgeCacheWriteAllocateRom, 0x06, 0x6201); // MOV.W @R0,R2
    WriteWord(lowCartridgeCacheWriteAllocateRom, 0x08, 0x001B); // SLEEP
    WriteLong(lowCartridgeCacheWriteAllocateRom, 0x18, 0x0000_0040);
    WriteLong(lowCartridgeCacheWriteAllocateRom, 0x1C, 0x0000_A55A);
    ThirtyTwoXDevice lowCartridgeCacheWriteAllocateDevice = new(lowCartridgeCacheWriteAllocateRom);
    lowCartridgeCacheWriteAllocateDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    WriteSh2ByteForTest(lowCartridgeCacheWriteAllocateDevice, 0xFFFF_FE92, 0x01);
    lowCartridgeCacheWriteAllocateDevice.MasterSh2.Run(12);
    AssertEqual(0x0000_0000u, lowCartridgeCacheWriteAllocateDevice.MasterSh2.R[2]);

    byte[] dreqDmaRom = new byte[0x100];
    int dreqPc = 0;
    int dreqLiteral = 0x40;
    void EmitMovLongLiteral(int register, uint value)
    {
        WriteLong(dreqDmaRom, dreqLiteral, value);
        int baseAddress = (dreqPc + 4) & ~3;
        int displacement = (dreqLiteral - baseAddress) / 4;
        WriteWord(dreqDmaRom, dreqPc, (ushort)(0xD000 | (register << 8) | displacement));
        dreqPc += 2;
        dreqLiteral += 4;
    }

    void EmitStoreR1AtR0()
    {
        WriteWord(dreqDmaRom, dreqPc, 0x2012); // MOV.L R1,@R0
        dreqPc += 2;
    }

    EmitMovLongLiteral(0, 0xFFFF_FF80); // SH-2 DMA source register 0
    EmitMovLongLiteral(1, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    EmitStoreR1AtR0();
    EmitMovLongLiteral(0, 0xFFFF_FF84); // SH-2 DMA destination register 0
    EmitMovLongLiteral(1, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x100);
    EmitStoreR1AtR0();
    EmitMovLongLiteral(0, 0xFFFF_FF88); // SH-2 DMA transfer count register 0
    EmitMovLongLiteral(1, 4);
    EmitStoreR1AtR0();
    EmitMovLongLiteral(0, 0xFFFF_FF8C); // SH-2 DMA channel control register 0
    EmitMovLongLiteral(1, 0x0000_0001);
    EmitStoreR1AtR0();
    EmitMovLongLiteral(0, 0xFFFF_FFB0); // SH-2 DMA operation register
    EmitMovLongLiteral(1, 0x0000_0001);
    EmitStoreR1AtR0();
    WriteWord(dreqDmaRom, dreqPc, 0x001B); // SLEEP

    ThirtyTwoXDevice dreqDmaDevice = new(dreqDmaRom);
    dreqDmaDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    dreqDmaDevice.MasterSh2.Run(64);
    dreqDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset, 0x0004);
    dreqDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset, 0x0004);
    dreqDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0x1020);
    dreqDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0x3040);
    dreqDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0x5060);
    dreqDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0x7080);
    AssertEqual((byte)0x10, dreqDmaDevice.Sdram[0x100]);
    AssertEqual((byte)0x20, dreqDmaDevice.Sdram[0x101]);
    AssertEqual((byte)0x70, dreqDmaDevice.Sdram[0x106]);
    AssertEqual((byte)0x80, dreqDmaDevice.Sdram[0x107]);
    AssertEqual((ushort)0x0000, dreqDmaDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x4000, ReadSh2WordForTest(dreqDmaDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));

    ThirtyTwoXDevice gatedDreqDmaDevice = new(dreqDmaRom);
    gatedDreqDmaDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    gatedDreqDmaDevice.MasterSh2.Run(64);
    WriteSh2ByteForTest(gatedDreqDmaDevice, 0xFFFF_FE71, 0x01); // RXI request source, not DREQ.
    gatedDreqDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset, 0x0004);
    gatedDreqDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset, 0x0002);
    gatedDreqDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0xAAAA);
    gatedDreqDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqFifoOffset, 0xBBBB);
    AssertEqual(2, gatedDreqDmaDevice.DreqFifoCount);
    AssertEqual((byte)0x00, gatedDreqDmaDevice.Sdram[0x100]);
    WriteSh2ByteForTest(gatedDreqDmaDevice, 0xFFFF_FE71, 0x00); // DREQ request source.
    AssertEqual(0, gatedDreqDmaDevice.DreqFifoCount);
    AssertEqual((byte)0xAA, gatedDreqDmaDevice.Sdram[0x100]);
    AssertEqual((byte)0xBB, gatedDreqDmaDevice.Sdram[0x102]);

    ThirtyTwoXDevice dreqSnoopDmaDevice = new(dreqDmaRom);
    dreqSnoopDmaDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    dreqSnoopDmaDevice.MasterSh2.Run(64);
    dreqSnoopDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqSourceAddressOffset, 0x0000);
    dreqSnoopDmaDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.DreqSourceAddressOffset + 2), 0x0200);
    dreqSnoopDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset, 0x0004);
    dreqSnoopDmaDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset, 0x0001);
    dreqSnoopDmaDevice.SnoopM68kVdpDmaWord(0x0000_0200, 0x1234);
    dreqSnoopDmaDevice.SnoopM68kVdpDmaWord(0x0000_0202, 0x5678);
    dreqSnoopDmaDevice.SnoopM68kVdpDmaWord(0x0000_0204, 0x9ABC);
    dreqSnoopDmaDevice.SnoopM68kVdpDmaWord(0x0000_0206, 0xDEF0);
    AssertEqual((byte)0x12, dreqSnoopDmaDevice.Sdram[0x100]);
    AssertEqual((byte)0x34, dreqSnoopDmaDevice.Sdram[0x101]);
    AssertEqual((byte)0xDE, dreqSnoopDmaDevice.Sdram[0x106]);
    AssertEqual((byte)0xF0, dreqSnoopDmaDevice.Sdram[0x107]);
    AssertEqual((ushort)0x0000, dreqSnoopDmaDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset));
    AssertEqual((ushort)0x4000, ReadSh2WordForTest(dreqSnoopDmaDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));

    ThirtyTwoXDevice dreqBackpressureDevice = new();
    dreqBackpressureDevice.Reset();
    dreqBackpressureDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqSourceAddressOffset, 0x0000);
    dreqBackpressureDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.DreqSourceAddressOffset + 2), 0x0300);
    dreqBackpressureDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset, 0x0008);
    dreqBackpressureDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset, 0x0001);
    dreqBackpressureDevice.SnoopM68kVdpDmaWord(0x0000_0300, 0x1001);
    dreqBackpressureDevice.SnoopM68kVdpDmaWord(0x0000_0302, 0x1002);
    dreqBackpressureDevice.SnoopM68kVdpDmaWord(0x0000_0304, 0x1003);
    dreqBackpressureDevice.SnoopM68kVdpDmaWord(0x0000_0306, 0x1004);
    AssertEqual(4, dreqBackpressureDevice.DreqFifoCount);
    WriteSh2LongForTest(dreqBackpressureDevice, 0xFFFF_FF80, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqFifoOffset));
    WriteSh2LongForTest(dreqBackpressureDevice, 0xFFFF_FF84, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x180);
    WriteSh2LongForTest(dreqBackpressureDevice, 0xFFFF_FF88, 5);
    WriteSh2LongForTest(dreqBackpressureDevice, 0xFFFF_FF8C, 0x0000_0001);
    WriteSh2LongForTest(dreqBackpressureDevice, 0xFFFF_FFB0, 0x0000_0001);
    AssertEqual(0, dreqBackpressureDevice.DreqFifoCount);
    AssertEqual((ushort)0x0001, ReadSh2WordForTest(dreqBackpressureDevice, 0xFFFF_FF8A));
    AssertEqual((ushort)0x0001, ReadSh2WordForTest(dreqBackpressureDevice, 0xFFFF_FF8E));
    AssertEqual((ushort)0x4001, ReadSh2WordForTest(dreqBackpressureDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));
    AssertTrue(dreqBackpressureDevice.SnoopM68kVdpDmaWord(0x0000_0308, 0x1005), "DREQ backpressure should drain a full FIFO before accepting the next VDP DMA word");
    AssertEqual(0, dreqBackpressureDevice.DreqFifoCount);
    AssertEqual((ushort)0x0000, ReadSh2WordForTest(dreqBackpressureDevice, 0xFFFF_FF8A));
    AssertEqual((ushort)0x0002, ReadSh2WordForTest(dreqBackpressureDevice, 0xFFFF_FF8E));
    AssertEqual((byte)0x10, dreqBackpressureDevice.Sdram[0x180]);
    AssertEqual((byte)0x01, dreqBackpressureDevice.Sdram[0x181]);
    AssertEqual((byte)0x10, dreqBackpressureDevice.Sdram[0x188]);
    AssertEqual((byte)0x05, dreqBackpressureDevice.Sdram[0x189]);

    byte[] memoryDmaRom = new byte[0x100];
    WriteWord(memoryDmaRom, 0x80, 0xABCD);
    WriteWord(memoryDmaRom, 0x82, 0x1234);
    int memoryDmaPc = 0;
    int memoryDmaLiteral = 0x40;
    void EmitMemoryDmaMovLongLiteral(int register, uint value)
    {
        WriteLong(memoryDmaRom, memoryDmaLiteral, value);
        int baseAddress = (memoryDmaPc + 4) & ~3;
        int displacement = (memoryDmaLiteral - baseAddress) / 4;
        WriteWord(memoryDmaRom, memoryDmaPc, (ushort)(0xD000 | (register << 8) | displacement));
        memoryDmaPc += 2;
        memoryDmaLiteral += 4;
    }

    void EmitMemoryDmaStoreR1AtR0()
    {
        WriteWord(memoryDmaRom, memoryDmaPc, 0x2012); // MOV.L R1,@R0
        memoryDmaPc += 2;
    }

    EmitMemoryDmaMovLongLiteral(0, 0xFFFF_FF80);
    EmitMemoryDmaMovLongLiteral(1, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x80);
    EmitMemoryDmaStoreR1AtR0();
    EmitMemoryDmaMovLongLiteral(0, 0xFFFF_FF84);
    EmitMemoryDmaMovLongLiteral(1, ThirtyTwoXHardwareProfile.Sh2FrameBufferCachedStart + 0x200);
    EmitMemoryDmaStoreR1AtR0();
    EmitMemoryDmaMovLongLiteral(0, 0xFFFF_FF88);
    EmitMemoryDmaMovLongLiteral(1, 2);
    EmitMemoryDmaStoreR1AtR0();
    EmitMemoryDmaMovLongLiteral(0, 0xFFFF_FF8C);
    EmitMemoryDmaMovLongLiteral(1, 0x0000_56E1);
    EmitMemoryDmaStoreR1AtR0();
    EmitMemoryDmaMovLongLiteral(0, 0xFFFF_FFB0);
    EmitMemoryDmaMovLongLiteral(1, 1);
    EmitMemoryDmaStoreR1AtR0();
    WriteWord(memoryDmaRom, memoryDmaPc, 0x001B);

    ThirtyTwoXDevice memoryDmaDevice = new(memoryDmaRom);
    memoryDmaDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    memoryDmaDevice.RestoreState(memoryDmaDevice.CaptureState() with { VdpAccessGrantedToSh2 = true });
    memoryDmaDevice.MasterSh2.Run(64);
    AssertEqual((byte)0xAB, memoryDmaDevice.DrawFrameBuffer[0x200]);
    AssertEqual((byte)0xCD, memoryDmaDevice.DrawFrameBuffer[0x201]);
    AssertEqual((byte)0x12, memoryDmaDevice.DrawFrameBuffer[0x202]);
    AssertEqual((byte)0x34, memoryDmaDevice.DrawFrameBuffer[0x203]);

    byte[] systemRegisterDmaRom = new byte[0x100];
    WriteWord(systemRegisterDmaRom, 0x80, 0x0004);
    int systemRegisterDmaPc = 0;
    int systemRegisterDmaLiteral = 0x40;
    void EmitSystemRegisterDmaMovLongLiteral(int register, uint value)
    {
        WriteLong(systemRegisterDmaRom, systemRegisterDmaLiteral, value);
        int baseAddress = (systemRegisterDmaPc + 4) & ~3;
        int displacement = (systemRegisterDmaLiteral - baseAddress) / 4;
        WriteWord(systemRegisterDmaRom, systemRegisterDmaPc, (ushort)(0xD000 | (register << 8) | displacement));
        systemRegisterDmaPc += 2;
        systemRegisterDmaLiteral += 4;
    }

    void EmitSystemRegisterDmaStoreR1AtR0()
    {
        WriteWord(systemRegisterDmaRom, systemRegisterDmaPc, 0x2012); // MOV.L R1,@R0
        systemRegisterDmaPc += 2;
    }

    EmitSystemRegisterDmaMovLongLiteral(0, 0xFFFF_FF80);
    EmitSystemRegisterDmaMovLongLiteral(1, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x80);
    EmitSystemRegisterDmaStoreR1AtR0();
    EmitSystemRegisterDmaMovLongLiteral(0, 0xFFFF_FF84);
    EmitSystemRegisterDmaMovLongLiteral(1, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset));
    EmitSystemRegisterDmaStoreR1AtR0();
    EmitSystemRegisterDmaMovLongLiteral(0, 0xFFFF_FF88);
    EmitSystemRegisterDmaMovLongLiteral(1, 1);
    EmitSystemRegisterDmaStoreR1AtR0();
    EmitSystemRegisterDmaMovLongLiteral(0, 0xFFFF_FF8C);
    EmitSystemRegisterDmaMovLongLiteral(1, 0x0000_56E1);
    EmitSystemRegisterDmaStoreR1AtR0();
    EmitSystemRegisterDmaMovLongLiteral(0, 0xFFFF_FFB0);
    EmitSystemRegisterDmaMovLongLiteral(1, 1);
    EmitSystemRegisterDmaStoreR1AtR0();
    WriteWord(systemRegisterDmaRom, systemRegisterDmaPc, 0x001B);

    ThirtyTwoXDevice systemRegisterDmaDevice = new(systemRegisterDmaRom);
    systemRegisterDmaDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    systemRegisterDmaDevice.MasterSh2.Run(64);
    AssertEqual((ushort)0x0004, (ushort)(systemRegisterDmaDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset) & 0x3FFF));
    AssertTrue(systemRegisterDmaDevice.MasterSh2.Halted, "SH-2 DMA into 32X system registers should not recursively re-enter DMA");

    byte[] timerRom = new byte[0x100];
    WriteWord(timerRom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(timerRom, 0x02, 0x6101); // MOV.W @R0,R1
    WriteWord(timerRom, 0x04, 0x0009); // NOP
    WriteWord(timerRom, 0x06, 0x0009); // NOP
    WriteWord(timerRom, 0x08, 0x0009); // NOP
    WriteWord(timerRom, 0x0A, 0x0009); // NOP
    WriteWord(timerRom, 0x0C, 0x0009); // NOP
    WriteWord(timerRom, 0x0E, 0x0009); // NOP
    WriteWord(timerRom, 0x10, 0x6201); // MOV.W @R0,R2
    WriteWord(timerRom, 0x12, 0x001B); // SLEEP
    WriteLong(timerRom, 0x14, 0xFFFF_FE12);
    ThirtyTwoXDevice timerDevice = new(timerRom);
    timerDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    timerDevice.MasterSh2.Run(12);
    AssertTrue(timerDevice.MasterSh2.R[2] != timerDevice.MasterSh2.R[1], "SH-2 free-running timer should advance while code executes");

    byte[] timerStatusRom = new byte[0x80];
    WriteWord(timerStatusRom, 0x00, 0xD007); // MOV.L @(literal,PC),R0
    WriteWord(timerStatusRom, 0x02, 0x6100); // MOV.B @R0,R1
    WriteWord(timerStatusRom, 0x04, 0x7001); // ADD #1,R0
    WriteWord(timerStatusRom, 0x06, 0x6200); // MOV.B @R0,R2
    WriteWord(timerStatusRom, 0x08, 0x7001); // ADD #1,R0
    WriteWord(timerStatusRom, 0x0A, 0x6300); // MOV.B @R0,R3
    WriteWord(timerStatusRom, 0x0C, 0x7001); // ADD #1,R0
    WriteWord(timerStatusRom, 0x0E, 0x6400); // MOV.B @R0,R4
    WriteWord(timerStatusRom, 0x10, 0x001B); // SLEEP
    WriteLong(timerStatusRom, 0x20, 0xFFFF_FE10);
    ThirtyTwoXDevice timerStatusDevice = new(timerStatusRom);
    timerStatusDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    timerStatusDevice.MasterSh2.Run(16);
    AssertEqual(1u, timerStatusDevice.MasterSh2.R[1]);
    AssertEqual(0u, timerStatusDevice.MasterSh2.R[2]);
    AssertEqual(0u, timerStatusDevice.MasterSh2.R[3]);

    device.WritePaletteWord(0, 0x7FFF);
    AssertEqual((ushort)0x7FFF, device.ReadPaletteWord(0));

    ThirtyTwoXDevice fillDevice = new();
    fillDevice.Reset();
    fillDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.AutoFillLengthOffset, 0x0002);
    fillDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.AutoFillStartAddressOffset, 0x20FE);
    fillDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.AutoFillDataOffset, 0x1234);
    AssertEqual((byte)0x12, fillDevice.DrawFrameBuffer[0x41FC]);
    AssertEqual((byte)0x34, fillDevice.DrawFrameBuffer[0x41FD]);
    AssertEqual((byte)0x12, fillDevice.DrawFrameBuffer[0x41FE]);
    AssertEqual((byte)0x34, fillDevice.DrawFrameBuffer[0x41FF]);
    AssertEqual((byte)0x12, fillDevice.DrawFrameBuffer[0x4000]);
    AssertEqual((byte)0x34, fillDevice.DrawFrameBuffer[0x4001]);
    AssertEqual((ushort)0x2000, fillDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.AutoFillStartAddressOffset));
    AssertEqual(6, fillDevice.FrameBufferByteWriteCount);

    ThirtyTwoXDevice byteFillDevice = new();
    byteFillDevice.Reset();
    byteFillDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.AutoFillLengthOffset, 0x0000);
    byteFillDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.AutoFillStartAddressOffset, 0x0010);
    byteFillDevice.WriteVdpRegisterByte((ushort)(ThirtyTwoXHardwareProfile.AutoFillDataOffset + 0), 0x56);
    AssertEqual(0, byteFillDevice.FrameBufferByteWriteCount);
    byteFillDevice.WriteVdpRegisterByte((ushort)(ThirtyTwoXHardwareProfile.AutoFillDataOffset + 1), 0x78);
    AssertEqual((byte)0x56, byteFillDevice.DrawFrameBuffer[0x20]);
    AssertEqual((byte)0x78, byteFillDevice.DrawFrameBuffer[0x21]);
    AssertEqual(2, byteFillDevice.FrameBufferByteWriteCount);

    byte[] bankedRom = new byte[0x400000];
    bankedRom[0x000010] = 0x11;
    bankedRom[0x100010] = 0x22;
    ThirtyTwoXDevice bankedCacheDevice = new(bankedRom);
    bankedCacheDevice.Reset();
    AssertEqual((byte)0x11, ReadSh2ByteForTest(bankedCacheDevice, ThirtyTwoXHardwareProfile.Sh2CartridgeBankedCachedStart + 0x10));
    bankedCacheDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.BankSetOffset, 0x0001);
    AssertEqual((byte)0x22, ReadSh2ByteForTest(bankedCacheDevice, ThirtyTwoXHardwareProfile.Sh2CartridgeBankedCachedStart + 0x10));
    AssertEqual((byte)0x22, ReadSh2ByteForTest(bankedCacheDevice, ThirtyTwoXHardwareProfile.Sh2CartridgeBankedStart + 0x10));
    bankedCacheDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.BankSetOffset, 0x0000);
    AssertEqual((byte)0x11, ReadSh2ByteForTest(bankedCacheDevice, ThirtyTwoXHardwareProfile.Sh2CartridgeBankedStart + 0x10));

    ThirtyTwoXDevice overwriteDevice = new();
    overwriteDevice.Reset();
    overwriteDevice.WriteFrameBufferWord(0, 0x1234);
    overwriteDevice.WriteFrameBufferByte(0, 0x00);
    AssertEqual((byte)0x00, overwriteDevice.DrawFrameBuffer[0]);
    overwriteDevice.WriteFrameBufferByte(0, 0x12);
    overwriteDevice.WriteOverwriteImageWord(0, 0x00AB);
    AssertEqual((byte)0x12, overwriteDevice.DrawFrameBuffer[0]);
    AssertEqual((byte)0xAB, overwriteDevice.DrawFrameBuffer[1]);
    overwriteDevice.WriteOverwriteImageByte(1, 0x00);
    AssertEqual((byte)0xAB, overwriteDevice.DrawFrameBuffer[1]);

    ThirtyTwoXDevice frameBufferMirrorDevice = new();
    frameBufferMirrorDevice.Reset();
    frameBufferMirrorDevice.RestoreState(frameBufferMirrorDevice.CaptureState() with { VdpAccessGrantedToSh2 = true });
    WriteSh2ByteForTest(frameBufferMirrorDevice, 0x0500_0040, 0x66);
    AssertEqual((byte)0x66, frameBufferMirrorDevice.DrawFrameBuffer[0x40]);
    WriteSh2ByteForTest(frameBufferMirrorDevice, 0x0500_0040, 0x00);
    AssertEqual((byte)0x00, frameBufferMirrorDevice.DrawFrameBuffer[0x40]);
    WriteSh2ByteForTest(frameBufferMirrorDevice, 0x2502_0041, 0x77);
    AssertEqual((byte)0x77, frameBufferMirrorDevice.DrawFrameBuffer[0x41]);
    WriteSh2ByteForTest(frameBufferMirrorDevice, 0x2502_0041, 0x00);
    AssertEqual((byte)0x77, frameBufferMirrorDevice.DrawFrameBuffer[0x41]);

    ThirtyTwoXDevice runLengthDevice = new();
    runLengthDevice.Reset();
    runLengthDevice.WriteFrameBufferWord(0, 0x0100);
    runLengthDevice.WriteFrameBufferWord(0x200, 0x0304);
    runLengthDevice.WritePaletteWord(8, 0x03E0);
    runLengthDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0003);
    LatchThirtyTwoXVdp(runLengthDevice);
    runLengthDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset, 0x0001);
    byte[] runLengthFramebuffer = new byte[MdSharp.Core.Video.Vdp.ScreenWidth * MdSharp.Core.Video.Vdp.ScreenHeight * 3];
    runLengthDevice.CompositeFrameRgbInto(runLengthFramebuffer);
    AssertEqual((byte)0, runLengthFramebuffer[0]);
    AssertEqual((byte)255, runLengthFramebuffer[1]);
    AssertEqual((byte)0, runLengthFramebuffer[2]);
    AssertEqual((byte)0, runLengthFramebuffer[9]);
    AssertEqual((byte)255, runLengthFramebuffer[10]);
    AssertEqual((byte)0, runLengthFramebuffer[11]);

    ThirtyTwoXDevice runLengthTerminatorDevice = new();
    runLengthTerminatorDevice.Reset();
    runLengthTerminatorDevice.WriteFrameBufferWord(0, 0x0100);
    runLengthTerminatorDevice.WriteFrameBufferWord(0x200, 0x0001);
    runLengthTerminatorDevice.WriteFrameBufferWord(0x202, 0x0000);
    runLengthTerminatorDevice.WriteFrameBufferWord(0x204, 0xFF02);
    runLengthTerminatorDevice.WritePaletteWord(2, 0x001F);
    runLengthTerminatorDevice.WritePaletteWord(4, 0x03E0);
    runLengthTerminatorDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0003);
    LatchThirtyTwoXVdp(runLengthTerminatorDevice);
    runLengthTerminatorDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset, 0x0001);
    byte[] runLengthTerminatorFramebuffer = new byte[MdSharp.Core.Video.Vdp.ScreenWidth * MdSharp.Core.Video.Vdp.ScreenHeight * 3];
    runLengthTerminatorDevice.CompositeFrameRgbInto(runLengthTerminatorFramebuffer);
    AssertEqual((byte)255, runLengthTerminatorFramebuffer[0]);
    AssertEqual((byte)0, runLengthTerminatorFramebuffer[1]);
    AssertEqual((byte)0, runLengthTerminatorFramebuffer[2]);
    AssertEqual((byte)0, runLengthTerminatorFramebuffer[3]);
    AssertEqual((byte)0, runLengthTerminatorFramebuffer[4]);
    AssertEqual((byte)0, runLengthTerminatorFramebuffer[5]);
    AssertEqual((byte)0, runLengthTerminatorFramebuffer[6]);
    AssertEqual((byte)255, runLengthTerminatorFramebuffer[7]);
    AssertEqual((byte)0, runLengthTerminatorFramebuffer[8]);

    AssertEqual(1, device.DrawFrameBufferIndex);
    device.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset, 0x0001);
    device.StepScanline(224, pal: false);
    AssertEqual(0, device.DrawFrameBufferIndex);
    AssertEqual(ThirtyTwoXHardwareProfile.FrameBufferBytes, device.DrawFrameBuffer.Length);
    AssertEqual(ThirtyTwoXHardwareProfile.FrameBufferBytes, device.DisplayFrameBuffer.Length);

    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x8083);
    AssertTrue(device.AdapterEnabled, "ADEN should enable 32X adapter state");
    AssertTrue(device.Sh2ResetEnabled, "REN should arm SH-2 reset control");
    AssertTrue(device.Sh2ResetReleased, "RES should release SH-2 reset");
    AssertTrue(device.VdpAccessGrantedToSh2, "FM should grant 32X VDP access to SH-2");
    AssertTrue(!device.Sh2HeldInReset, "adapter control should release SH-2 execution");

    ThirtyTwoXDevice compositeDevice = new();
    compositeDevice.Reset();
    compositeDevice.WriteFrameBufferWord(0, 0x0100);
    compositeDevice.WriteFrameBufferByte(0x200, 2);
    compositeDevice.WritePaletteWord(4, 0x001F);
    compositeDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);
    LatchThirtyTwoXVdp(compositeDevice);
    compositeDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset, 0x0001);
    byte[] framebuffer = new byte[MdSharp.Core.Video.Vdp.ScreenWidth * MdSharp.Core.Video.Vdp.ScreenHeight * 3];
    compositeDevice.CompositeFrameRgbInto(framebuffer);
    AssertEqual((byte)255, framebuffer[0]);
    AssertEqual((byte)0, framebuffer[1]);
    AssertEqual((byte)0, framebuffer[2]);
    AssertTrue(compositeDevice.LastCompositeUsedFallback, "compositor should report diagnostic draw-buffer fallback");

    ThirtyTwoXDevice lineTableDevice = new();
    lineTableDevice.Reset();
    lineTableDevice.WriteFrameBufferWord(0, 0x0100);
    lineTableDevice.WriteFrameBufferWord(2, 0x0200);
    lineTableDevice.WriteFrameBufferByte(0x200, 1);
    lineTableDevice.WriteFrameBufferByte(0x400, 2);
    lineTableDevice.WritePaletteWord(2, 0x001F);
    lineTableDevice.WritePaletteWord(4, 0x03E0);
    lineTableDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);
    LatchThirtyTwoXVdp(lineTableDevice);
    lineTableDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset, 0x0001);
    byte[] lineTableFramebuffer = new byte[MdSharp.Core.Video.Vdp.ScreenWidth * MdSharp.Core.Video.Vdp.ScreenHeight * 3];
    lineTableDevice.CompositeFrameRgbInto(lineTableFramebuffer);
    AssertEqual((byte)255, lineTableFramebuffer[0]);
    AssertEqual((byte)0, lineTableFramebuffer[1]);
    AssertEqual((byte)0, lineTableFramebuffer[2]);
    int secondLineOffset = MdSharp.Core.Video.Vdp.ScreenWidth * 3;
    AssertEqual((byte)0, lineTableFramebuffer[secondLineOffset]);
    AssertEqual((byte)255, lineTableFramebuffer[secondLineOffset + 1]);
    AssertEqual((byte)0, lineTableFramebuffer[secondLineOffset + 2]);

    ThirtyTwoXDevice directColorLineTableDevice = new();
    directColorLineTableDevice.Reset();
    directColorLineTableDevice.WriteFrameBufferWord(0, 0x0100);
    directColorLineTableDevice.WriteFrameBufferWord(0x200, 0x001F);
    directColorLineTableDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0002);
    LatchThirtyTwoXVdp(directColorLineTableDevice);
    directColorLineTableDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset, 0x0001);
    byte[] directColorLineTableFramebuffer = new byte[MdSharp.Core.Video.Vdp.ScreenWidth * MdSharp.Core.Video.Vdp.ScreenHeight * 3];
    directColorLineTableDevice.CompositeFrameRgbInto(directColorLineTableFramebuffer);
    AssertEqual((byte)255, directColorLineTableFramebuffer[0]);
    AssertEqual((byte)0, directColorLineTableFramebuffer[1]);
    AssertEqual((byte)0, directColorLineTableFramebuffer[2]);

    ThirtyTwoXDevice shiftedDevice = new();
    shiftedDevice.Reset();
    shiftedDevice.WriteFrameBufferWord(0, 0x0100);
    shiftedDevice.WriteFrameBufferByte(0x200, 1);
    shiftedDevice.WriteFrameBufferByte(0x201, 2);
    shiftedDevice.WritePaletteWord(2, 0x001F);
    shiftedDevice.WritePaletteWord(4, 0x03E0);
    shiftedDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);
    shiftedDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.ScreenShiftControlOffset, 0x0001);
    LatchThirtyTwoXVdp(shiftedDevice);
    shiftedDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset, 0x0001);
    byte[] shiftedFramebuffer = new byte[MdSharp.Core.Video.Vdp.ScreenWidth * MdSharp.Core.Video.Vdp.ScreenHeight * 3];
    shiftedDevice.CompositeFrameRgbInto(shiftedFramebuffer);
    AssertEqual((byte)0, shiftedFramebuffer[0]);
    AssertEqual((byte)255, shiftedFramebuffer[1]);
    AssertEqual((byte)0, shiftedFramebuffer[2]);

    ThirtyTwoXDevice priorityDevice = new();
    priorityDevice.Reset();
    priorityDevice.WriteFrameBufferWord(0, 0x0100);
    priorityDevice.WriteFrameBufferByte(0x200, 1);
    priorityDevice.WritePaletteWord(2, 0x001F);
    priorityDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);
    LatchThirtyTwoXVdp(priorityDevice);
    priorityDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset, 0x0001);
    byte[] priorityFramebuffer = new byte[MdSharp.Core.Video.Vdp.ScreenWidth * MdSharp.Core.Video.Vdp.ScreenHeight * 3];
    priorityFramebuffer[0] = 9;
    priorityFramebuffer[1] = 8;
    priorityFramebuffer[2] = 7;
    bool[] mdOpaque = new bool[MdSharp.Core.Video.Vdp.ScreenWidth * MdSharp.Core.Video.Vdp.ScreenHeight];
    mdOpaque[0] = true;
    priorityDevice.CompositeFrameRgbInto(priorityFramebuffer, mdOpaque);
    AssertEqual((byte)9, priorityFramebuffer[0]);
    AssertEqual((byte)8, priorityFramebuffer[1]);
    AssertEqual((byte)7, priorityFramebuffer[2]);

    priorityDevice.WritePaletteWord(2, 0x801F);
    priorityDevice.CompositeFrameRgbInto(priorityFramebuffer, mdOpaque);
    AssertEqual((byte)255, priorityFramebuffer[0]);
    AssertEqual((byte)0, priorityFramebuffer[1]);
    AssertEqual((byte)0, priorityFramebuffer[2]);

    priorityDevice.WritePaletteWord(2, 0x001F);
    priorityDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0081);
    LatchThirtyTwoXVdp(priorityDevice);
    priorityFramebuffer[0] = 9;
    priorityFramebuffer[1] = 8;
    priorityFramebuffer[2] = 7;
    priorityDevice.CompositeFrameRgbInto(priorityFramebuffer, mdOpaque);
    AssertEqual((byte)255, priorityFramebuffer[0]);
    AssertEqual((byte)0, priorityFramebuffer[1]);
    AssertEqual((byte)0, priorityFramebuffer[2]);

    priorityDevice.WritePaletteWord(2, 0x801F);
    priorityFramebuffer[0] = 9;
    priorityFramebuffer[1] = 8;
    priorityFramebuffer[2] = 7;
    priorityDevice.CompositeFrameRgbInto(priorityFramebuffer, mdOpaque);
    AssertEqual((byte)9, priorityFramebuffer[0]);
    AssertEqual((byte)8, priorityFramebuffer[1]);
    AssertEqual((byte)7, priorityFramebuffer[2]);

    ThirtyTwoXDevice swapDevice = new();
    swapDevice.Reset();
    swapDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);
    LatchThirtyTwoXVdp(swapDevice);
    swapDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset, 0x0001);
    AssertEqual(1, swapDevice.DrawFrameBufferIndex);
    AssertTrue(swapDevice.FrameBufferSwapPending, "display-time frame buffer writes should wait for VBlank");
    AssertTrue((swapDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset) & 0x0002) != 0, "FEN should report active-display frame-buffer engagement");
    swapDevice.StepScanline(ThirtyTwoXHardwareProfile.NtscVisibleLines, pal: false);
    AssertEqual(0, swapDevice.DrawFrameBufferIndex);
    AssertTrue(!swapDevice.FrameBufferSwapPending, "VBlank should complete the pending 32X frame buffer swap");
    ushort frameBufferStatus = swapDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset);
    AssertTrue((frameBufferStatus & 0x8000) != 0, "32X VBlank status should be visible in frame buffer control reads");

    ThirtyTwoXDevice readDevice = new();
    readDevice.Reset();
    readDevice.WriteFrameBufferWord(0, 0x1357);
    readDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);
    LatchThirtyTwoXVdp(readDevice);
    readDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset, 0x0001);
    readDevice.StepScanline(ThirtyTwoXHardwareProfile.NtscVisibleLines, pal: false);
    AssertEqual((ushort)0x0000, readDevice.ReadFrameBufferWord(0));
    readDevice.WriteFrameBufferWord(0, 0x2468);
    AssertEqual((ushort)0x2468, readDevice.ReadFrameBufferWord(0));
    AssertTrue((frameBufferStatus & 0x0002) == 0, "FEN should clear when frame-buffer access is approved after the swap");

    ThirtyTwoXDevice accessDevice = new();
    accessDevice.Reset();
    accessDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);
    AssertTrue((accessDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset) & 0x0002) != 0, "FEN should be visible while the 32X framebuffer is engaged during active display");

    ThirtyTwoXDevice latchDevice = new();
    latchDevice.Reset();
    latchDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);
    latchDevice.SetHBlank(true);
    latchDevice.SetHBlank(false);
    latchDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset, 0x0001);
    AssertEqual(0, latchDevice.DisplayFrameBufferIndex);
    AssertEqual(1, latchDevice.RequestedDisplayFrameBufferIndex);
    AssertTrue(latchDevice.FrameBufferSwapPending, "active display mode should defer frame-buffer selection until VBlank");
    AssertTrue((latchDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset) & 0x0001) != 0, "FS readback should preserve the requested frame buffer while the active display buffer is still pending");
    AssertTrue((latchDevice.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset) & 0x0002) != 0, "FEN should remain set during active display after a deferred frame-buffer selection");
    latchDevice.WriteFrameBufferWord(0, 0x55AA);
    AssertEqual((ushort)0x55AA, latchDevice.ReadFrameBufferWord(0));
    latchDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0000);
    latchDevice.SetHBlank(true);
    latchDevice.SetHBlank(false);
    AssertEqual(1, latchDevice.DisplayFrameBufferIndex);
    AssertTrue(!latchDevice.FrameBufferSwapPending, "latched blank mode should allow a pending frame-buffer selection to complete");
    latchDevice.WriteFrameBufferWord(0, 0x55AA);
    AssertEqual((ushort)0x55AA, latchDevice.DrawFrameBuffer[0] << 8 | latchDevice.DrawFrameBuffer[1]);

    ThirtyTwoXDevice sh2VdpAccessDevice = new();
    sh2VdpAccessDevice.Reset();
    WriteSh2WordForTest(sh2VdpAccessDevice, ThirtyTwoXHardwareProfile.Sh2FrameBufferStart, 0x1234);
    AssertEqual((ushort)0x0000, (ushort)((sh2VdpAccessDevice.DrawFrameBuffer[0] << 8) | sh2VdpAccessDevice.DrawFrameBuffer[1]));
    AssertEqual((ushort)0xFFFF, ReadSh2WordForTest(sh2VdpAccessDevice, ThirtyTwoXHardwareProfile.Sh2FrameBufferStart));
    WriteSh2WordForTest(sh2VdpAccessDevice, ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart, 0x5678);
    AssertEqual((ushort)0x0000, sh2VdpAccessDevice.ReadPaletteWord(0));
    AssertEqual((ushort)0xFFFF, ReadSh2WordForTest(sh2VdpAccessDevice, ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart));

    sh2VdpAccessDevice.RestoreState(sh2VdpAccessDevice.CaptureState() with { VdpAccessGrantedToSh2 = true });
    WriteSh2WordForTest(sh2VdpAccessDevice, ThirtyTwoXHardwareProfile.Sh2FrameBufferStart, 0x1234);
    AssertEqual((ushort)0x1234, (ushort)((sh2VdpAccessDevice.DrawFrameBuffer[0] << 8) | sh2VdpAccessDevice.DrawFrameBuffer[1]));
    WriteSh2WordForTest(sh2VdpAccessDevice, ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart, 0x5678);
    AssertEqual((ushort)0x5678, sh2VdpAccessDevice.ReadPaletteWord(0));

    ThirtyTwoXDevice sh2FenDevice = new();
    sh2FenDevice.Reset();
    sh2FenDevice.RestoreState(sh2FenDevice.CaptureState() with { VdpAccessGrantedToSh2 = true });
    sh2FenDevice.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);
    LatchThirtyTwoXVdp(sh2FenDevice);
    WriteSh2WordForTest(sh2FenDevice, ThirtyTwoXHardwareProfile.Sh2FrameBufferStart, 0x9ABC);
    AssertEqual((ushort)0x9ABC, (ushort)((sh2FenDevice.DrawFrameBuffer[0] << 8) | sh2FenDevice.DrawFrameBuffer[1]));
    AssertEqual((ushort)0x9ABC, ReadSh2WordForTest(sh2FenDevice, ThirtyTwoXHardwareProfile.Sh2FrameBufferStart));

    ThirtyTwoXDevice hInterruptDevice = new();
    hInterruptDevice.Reset();
    hInterruptDevice.RestoreState(hInterruptDevice.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        VdpAccessGrantedToSh2 = true,
        MasterInterruptMask = 0x0004,
        SlaveInterruptMask = 0x0004,
        HorizontalInterruptPeriod = 1,
    });
    hInterruptDevice.SetHBlank(true);
    AssertEqual(0, hInterruptDevice.MasterSh2.PendingInterruptLevel);
    hInterruptDevice.SetHBlank(false);
    hInterruptDevice.SetHBlank(true);
    AssertEqual(10, hInterruptDevice.MasterSh2.PendingInterruptLevel);
    AssertEqual(69, hInterruptDevice.MasterSh2.PendingInterruptVectorNumber);

    ThirtyTwoXDevice vblankHInterruptDevice = new();
    vblankHInterruptDevice.Reset();
    vblankHInterruptDevice.RestoreState(vblankHInterruptDevice.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        VdpAccessGrantedToSh2 = true,
        VBlank = true,
        MasterInterruptMask = 0x0004,
        HorizontalInterruptPeriod = 0,
    });
    vblankHInterruptDevice.SetHBlank(true);
    AssertEqual(0, vblankHInterruptDevice.MasterSh2.PendingInterruptLevel);
    vblankHInterruptDevice.RestoreState(vblankHInterruptDevice.CaptureState() with
    {
        MasterInterruptMask = 0x0024,
    });
    vblankHInterruptDevice.SetHBlank(false);
    vblankHInterruptDevice.SetHBlank(true);
    AssertEqual(10, vblankHInterruptDevice.MasterSh2.PendingInterruptLevel);

    byte[] acceptedInterruptRom = new byte[0x300];
    WriteWord(acceptedInterruptRom, 0x00, 0xE002); // MOV #2,R0
    WriteWord(acceptedInterruptRom, 0x02, 0x400E); // LDC R0,SR
    WriteWord(acceptedInterruptRom, 0x04, 0xAFFE); // BRA *
    WriteWord(acceptedInterruptRom, 0x06, 0x0009); // NOP
    WriteWord(acceptedInterruptRom, 0x80, 0xE156); // MOV #$56,R1
    WriteWord(acceptedInterruptRom, 0x82, 0x001B); // SLEEP
    WriteWord(acceptedInterruptRom, 0x90, 0xE257); // MOV #$57,R2
    WriteWord(acceptedInterruptRom, 0x92, 0x001B); // SLEEP
    WriteLong(acceptedInterruptRom, 0x40 + (69 * 4), ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x80);
    WriteLong(acceptedInterruptRom, 0x40 + (70 * 4), ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x90);

    ThirtyTwoXDevice acceptedHInterruptDevice = new(acceptedInterruptRom);
    acceptedHInterruptDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    acceptedHInterruptDevice.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x40);
    acceptedHInterruptDevice.MasterSh2.Run(3);
    acceptedHInterruptDevice.RestoreState(acceptedHInterruptDevice.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        VdpAccessGrantedToSh2 = true,
        MasterInterruptMask = 0x0004,
        HorizontalInterruptPeriod = 0,
    });
    acceptedHInterruptDevice.SetHBlank(true);
    acceptedHInterruptDevice.RunSh2Cycles(40);
    AssertEqual(0x0000_0056u, acceptedHInterruptDevice.MasterSh2.R[1]);
    AssertTrue(acceptedHInterruptDevice.CaptureState().MasterHorizontalInterruptPending, "accepted 32X H interrupt should remain latched until the clear register is written");

    ThirtyTwoXDevice acceptedVInterruptDevice = new(acceptedInterruptRom);
    acceptedVInterruptDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    acceptedVInterruptDevice.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x40);
    acceptedVInterruptDevice.MasterSh2.Run(3);
    acceptedVInterruptDevice.RestoreState(acceptedVInterruptDevice.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        VdpAccessGrantedToSh2 = true,
        MasterInterruptMask = 0x0008,
    });
    acceptedVInterruptDevice.StepScanline(ThirtyTwoXHardwareProfile.NtscVisibleLines, pal: false);
    acceptedVInterruptDevice.RunSh2Cycles(40);
    AssertEqual(0x0000_0057u, acceptedVInterruptDevice.MasterSh2.R[2]);
    AssertTrue(acceptedVInterruptDevice.CaptureState().MasterVerticalInterruptPending, "accepted 32X V interrupt should remain latched until the clear register is written");

    byte[] acceptedPwmInterruptRom = new byte[0x300];
    WriteWord(acceptedPwmInterruptRom, 0x00, 0xE002); // MOV #2,R0
    WriteWord(acceptedPwmInterruptRom, 0x02, 0x400E); // LDC R0,SR
    WriteWord(acceptedPwmInterruptRom, 0x04, 0xAFFE); // BRA *
    WriteWord(acceptedPwmInterruptRom, 0x06, 0x0009); // NOP
    WriteWord(acceptedPwmInterruptRom, 0x80, 0xE158); // MOV #$58,R1
    WriteWord(acceptedPwmInterruptRom, 0x82, 0x001B); // SLEEP
    WriteLong(acceptedPwmInterruptRom, 0x40 + (67 * 4), ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x80);
    ThirtyTwoXDevice acceptedPwmInterruptDevice = new(acceptedPwmInterruptRom);
    acceptedPwmInterruptDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    acceptedPwmInterruptDevice.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x40);
    acceptedPwmInterruptDevice.MasterSh2.Run(3);
    acceptedPwmInterruptDevice.RestoreState(acceptedPwmInterruptDevice.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        VdpAccessGrantedToSh2 = true,
        MasterInterruptMask = 0x0001,
    });
    acceptedPwmInterruptDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmControlOffset, 0x0105);
    acceptedPwmInterruptDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmCycleOffset, 0x0004);
    acceptedPwmInterruptDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, 0x0200);
    acceptedPwmInterruptDevice.RunSh2Cycles(40);
    AssertEqual(0x0000_0058u, acceptedPwmInterruptDevice.MasterSh2.R[1]);
}

void ThirtyTwoXSh2FrtInputCaptureSignal()
{
    byte[] rom = new byte[0x300];
    WriteWord(rom, 0x00, 0xD005); // MOV.L @(literal,PC),R0
    WriteWord(rom, 0x02, 0xE17F); // MOV #$7F,R1
    WriteWord(rom, 0x04, 0x2011); // MOV.W R1,@R0
    WriteWord(rom, 0x06, 0x001B); // SLEEP
    WriteLong(rom, 0x18, 0x2100_0000); // Slave SH-2 input capture signal.

    WriteWord(rom, 0x80, 0xD105); // MOV.L @(literal,PC),R1
    WriteWord(rom, 0x82, 0xE080); // MOV #$80,R0
    WriteWord(rom, 0x84, 0x2100); // MOV.B R0,@R1
    WriteWord(rom, 0x86, 0xE002); // MOV #2,R0
    WriteWord(rom, 0x88, 0x400E); // LDC R0,SR
    WriteWord(rom, 0x8A, 0xAFFE); // BRA *
    WriteWord(rom, 0x8C, 0x0009); // NOP
    WriteLong(rom, 0x98, 0xFFFF_FE10); // FRT TIER.

    WriteLong(rom, 0x100, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x180);
    WriteWord(rom, 0x180, 0xE255); // MOV #$55,R2
    WriteWord(rom, 0x182, 0x001B); // SLEEP

    ThirtyTwoXDevice disabledDevice = new(rom);
    disabledDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    disabledDevice.MasterSh2.Run(4);
    AssertEqual(0, disabledDevice.SlaveSh2.PendingInterruptLevel);

    ThirtyTwoXDevice device = new(rom);
    device.MasterSh2.Reset(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    device.SlaveSh2.Reset(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x80);
    device.SlaveSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    device.SlaveSh2.Run(8);
    AssertEqual(0x0000_0002u, device.SlaveSh2.SR & 0xFFu);

    device.MasterSh2.Run(4);
    AssertEqual(15, device.SlaveSh2.PendingInterruptLevel);
    AssertEqual(64, device.SlaveSh2.PendingInterruptVectorNumber);
    device.SlaveSh2.Run(4);
    AssertEqual(0x0000_0055u, device.SlaveSh2.R[2]);
    AssertTrue(device.SlaveSh2.Halted, "input capture should vector through FRT vector 64 when ICIE is enabled");
}

void ThirtyTwoXSh2FrtCounterWritesAndFlags()
{
    byte[] rom = new byte[0x100];
    for (int offset = 0; offset < rom.Length; offset += 2)
    {
        WriteWord(rom, offset, 0x0009); // NOP
    }

    ThirtyTwoXDevice device = new(rom);
    device.ResetSh2(0, 0);
    device.RestoreState(device.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
    });

    WriteSh2WordForTest(device, 0xFFFF_FE12, 0x1234);
    AssertEqual((ushort)0x1234, ReadSh2WordForTest(device, 0xFFFF_FE12));

    device.RunSh2Cycles(64);
    ushort advanced = ReadSh2WordForTest(device, 0xFFFF_FE12);
    AssertTrue(advanced > 0x1234, "FRC should advance from the CPU-written counter value");

    ThirtyTwoXDevice flagsDevice = new(rom);
    flagsDevice.ResetSh2(0, 0);
    flagsDevice.RestoreState(flagsDevice.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
    });

    WriteSh2WordForTest(flagsDevice, 0xFFFF_FE12, 0xFFFC);
    WriteSh2WordForTest(flagsDevice, 0xFFFF_FE14, 0xFFFF);
    flagsDevice.RunSh2Cycles(64);

    byte ftcsr = ReadSh2ByteForTest(flagsDevice, 0xFFFF_FE11);
    AssertTrue((ftcsr & 0x08) != 0, "FRT should set OCFA when FRC crosses OCRA");
    AssertTrue((ftcsr & 0x02) != 0, "FRT should set OVF when FRC wraps");

    ThirtyTwoXDevice compareBDevice = new(rom);
    compareBDevice.ResetSh2(0, 0);
    compareBDevice.RestoreState(compareBDevice.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
    });

    WriteSh2ByteForTest(compareBDevice, 0xFFFF_FE60, 0x07); // FRT priority in IPRB.
    WriteSh2ByteForTest(compareBDevice, 0xFFFF_FE67, 0x22); // FRT output-compare vector in VCRC.
    WriteSh2ByteForTest(compareBDevice, 0xFFFF_FE17, 0xF0); // Select OCRB.
    WriteSh2WordForTest(compareBDevice, 0xFFFF_FE14, 0x0002);
    WriteSh2ByteForTest(compareBDevice, 0xFFFF_FE10, 0x05); // Enable OCIB.
    WriteSh2WordForTest(compareBDevice, 0xFFFF_FE12, 0x0000);
    compareBDevice.RunSh2Cycles(32);

    byte compareBFtcsr = ReadSh2ByteForTest(compareBDevice, 0xFFFF_FE11);
    AssertTrue((compareBFtcsr & 0x04) != 0, "FRT should set OCFB when FRC crosses OCRB");
    AssertEqual(7, compareBDevice.MasterSh2.PendingInterruptLevel);
    AssertEqual(0x22, compareBDevice.MasterSh2.PendingInterruptVectorNumber);
}

void ThirtyTwoXSh2SciByteTransfer()
{
    byte[] rom = new byte[0x100];
    ThirtyTwoXDevice device = new(rom);
    device.ResetSh2(0, 0);
    device.RestoreState(device.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
    });

    AssertEqual((byte)0x84, ReadSh2ByteForTest(device, 0xFFFF_FE04, cpuIndex: 0));
    AssertEqual((byte)0x84, ReadSh2ByteForTest(device, 0xFFFF_FE04, cpuIndex: 1));

    WriteSh2ByteForTest(device, 0xFFFF_FE03, 0x5A, cpuIndex: 0);
    AssertEqual((byte)0x84, ReadSh2ByteForTest(device, 0xFFFF_FE04, cpuIndex: 0));
    AssertEqual((byte)0xC4, ReadSh2ByteForTest(device, 0xFFFF_FE04, cpuIndex: 1));
    AssertEqual((byte)0x5A, ReadSh2ByteForTest(device, 0xFFFF_FE05, cpuIndex: 1));

    WriteSh2ByteForTest(device, 0xFFFF_FE04, 0x80, cpuIndex: 1);
    AssertEqual((byte)0x84, ReadSh2ByteForTest(device, 0xFFFF_FE04, cpuIndex: 1));
}

void ThirtyTwoXSh2CoreExecutesSyntheticCode()
{
    static byte ReadSh2ByteForTest(ThirtyTwoXDevice target, uint address, int cpuIndex = 0)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Byte helper was not found");
        return (byte)method.Invoke(target, [address, cpuIndex])!;
    }

    static void WriteSh2ByteForTest(ThirtyTwoXDevice target, uint address, byte value, int cpuIndex = 0)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Byte helper was not found");
        method.Invoke(target, [address, value, cpuIndex]);
    }

    static uint ReadSh2LongForTest(ThirtyTwoXDevice target, uint address)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Long", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Long helper was not found");
        return (uint)method.Invoke(target, [address, 0])!;
    }

    static void WriteSh2LongForTest(ThirtyTwoXDevice target, uint address, uint value)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Long", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Long helper was not found");
        method.Invoke(target, [address, value, 0]);
    }

    byte[] rom = new byte[0x100];
    WriteWord(rom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(rom, 0x02, 0xE15A); // MOV #$5A,R1
    WriteWord(rom, 0x04, 0x1010); // MOV.L R1,@(0,R0)
    WriteWord(rom, 0x06, 0x5200); // MOV.L @(0,R0),R2
    WriteWord(rom, 0x08, 0x001B); // SLEEP
    WriteLong(rom, 0x14, ThirtyTwoXHardwareProfile.Sh2SdramStart);

    ThirtyTwoXDevice device = new(rom);
    device.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    int executed = device.MasterSh2.Run(16);

    AssertEqual(5, executed);
    AssertTrue(device.MasterSh2.Halted, "SH-2 synthetic program should halt on SLEEP");
    AssertEqual(0x0000_005Au, device.MasterSh2.R[2]);
    AssertEqual((byte)0x5A, device.Sdram[3]);

    byte[] cacheDataRom = new byte[0x100];
    WriteWord(cacheDataRom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(cacheDataRom, 0x02, 0xE134); // MOV #$34,R1
    WriteWord(cacheDataRom, 0x04, 0x1010); // MOV.L R1,@(0,R0)
    WriteWord(cacheDataRom, 0x06, 0x5200); // MOV.L @(0,R0),R2
    WriteWord(cacheDataRom, 0x08, 0x001B); // SLEEP
    WriteLong(cacheDataRom, 0x14, 0xC000_0000);
    ThirtyTwoXDevice cacheDataDevice = new(cacheDataRom);
    cacheDataDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    cacheDataDevice.MasterSh2.Run(16);
    AssertEqual(0x0000_0034u, cacheDataDevice.MasterSh2.R[2]);
    AssertEqual((byte)0x00, cacheDataDevice.Sdram[3]);
    ThirtyTwoXDevice.ThirtyTwoXState cacheDataState = cacheDataDevice.CaptureState();
    cacheDataDevice.Reset();
    cacheDataDevice.RestoreState(cacheDataState);
    cacheDataDevice.MasterSh2.Reset(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 6);
    cacheDataDevice.MasterSh2.R[0] = 0xC000_0000;
    cacheDataDevice.MasterSh2.Run(4);
    AssertEqual(0x0000_0034u, cacheDataDevice.MasterSh2.R[2]);
    WriteSh2ByteForTest(cacheDataDevice, 0xDFFF_FFFF, 0x7B);
    AssertEqual((byte)0x7B, ReadSh2ByteForTest(cacheDataDevice, 0xC000_0FFF));

    byte[] cacheControlRom = new byte[0x100];
    cacheControlRom[0x20] = 0x42;
    ThirtyTwoXDevice cacheControlDevice = new(cacheControlRom);
    uint cachedRomByte = ThirtyTwoXHardwareProfile.Sh2CartridgeFixedCachedStart + 0x20;
    WriteSh2ByteForTest(cacheControlDevice, 0xFFFF_FE92, 0x01);
    AssertEqual((byte)0x42, ReadSh2ByteForTest(cacheControlDevice, cachedRomByte));
    WriteSh2ByteForTest(cacheControlDevice, cachedRomByte, 0x99);
    AssertEqual((byte)0x99, ReadSh2ByteForTest(cacheControlDevice, cachedRomByte));
    WriteSh2ByteForTest(cacheControlDevice, 0xFFFF_FE92, 0x19);
    AssertEqual((byte)0x09, ReadSh2ByteForTest(cacheControlDevice, 0xFFFF_FE92));
    AssertEqual((byte)0x42, ReadSh2ByteForTest(cacheControlDevice, cachedRomByte));
    WriteSh2ByteForTest(cacheControlDevice, 0xC000_0020, 0x66);
    AssertEqual((byte)0x42, ReadSh2ByteForTest(cacheControlDevice, cachedRomByte));
    WriteSh2ByteForTest(cacheControlDevice, 0xFFFF_FE92, 0x05);
    WriteSh2ByteForTest(cacheControlDevice, cachedRomByte, 0x77);
    AssertEqual((byte)0x42, ReadSh2ByteForTest(cacheControlDevice, cachedRomByte));
    ThirtyTwoXDevice.ThirtyTwoXState cacheControlState = cacheControlDevice.CaptureState();
    cacheControlDevice.Reset();
    cacheControlDevice.RestoreState(cacheControlState);
    AssertEqual((byte)0x42, ReadSh2ByteForTest(cacheControlDevice, cachedRomByte));
    WriteSh2ByteForTest(cacheControlDevice, 0xFFFF_FE92, 0x19);
    AssertEqual((byte)0x42, ReadSh2ByteForTest(cacheControlDevice, cachedRomByte));

    ThirtyTwoXDevice sdramCacheIsolationDevice = new(cacheControlRom);
    uint cachedSdramByte = ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x20;
    uint cacheThroughSdramByte = ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart + 0x20;
    WriteSh2ByteForTest(sdramCacheIsolationDevice, cacheThroughSdramByte, 0x11, cpuIndex: 0);
    WriteSh2ByteForTest(sdramCacheIsolationDevice, 0xFFFF_FE92, 0x01, cpuIndex: 0);
    WriteSh2ByteForTest(sdramCacheIsolationDevice, 0xFFFF_FE92, 0x01, cpuIndex: 1);
    AssertEqual((byte)0x11, ReadSh2ByteForTest(sdramCacheIsolationDevice, cachedSdramByte, cpuIndex: 0));
    AssertEqual((byte)0x11, ReadSh2ByteForTest(sdramCacheIsolationDevice, cachedSdramByte, cpuIndex: 1));
    WriteSh2ByteForTest(sdramCacheIsolationDevice, cachedSdramByte, 0x22, cpuIndex: 0);
    AssertEqual((byte)0x22, ReadSh2ByteForTest(sdramCacheIsolationDevice, cachedSdramByte, cpuIndex: 0));
    AssertEqual((byte)0x11, ReadSh2ByteForTest(sdramCacheIsolationDevice, cachedSdramByte, cpuIndex: 1));
    AssertEqual((byte)0x22, ReadSh2ByteForTest(sdramCacheIsolationDevice, cacheThroughSdramByte, cpuIndex: 1));
    WriteSh2ByteForTest(sdramCacheIsolationDevice, cacheThroughSdramByte, 0x33, cpuIndex: 0);
    AssertEqual((byte)0x22, ReadSh2ByteForTest(sdramCacheIsolationDevice, cachedSdramByte, cpuIndex: 0));
    AssertEqual((byte)0x11, ReadSh2ByteForTest(sdramCacheIsolationDevice, cachedSdramByte, cpuIndex: 1));
    AssertEqual((byte)0x33, ReadSh2ByteForTest(sdramCacheIsolationDevice, cacheThroughSdramByte, cpuIndex: 1));

    ThirtyTwoXDevice cacheAddressArrayDevice = new(cacheControlRom);
    WriteSh2ByteForTest(cacheAddressArrayDevice, 0xFFFF_FE92, 0xC1); // Select way 3 and enable cache.
    WriteSh2LongForTest(cacheAddressArrayDevice, 0x6000_0004, 0x048C_00C0); // Address bit 2 marks valid; LRU is written from bits 11:6.
    AssertEqual(0x048C_0032u, ReadSh2LongForTest(cacheAddressArrayDevice, 0x6000_0000));
    WriteSh2LongForTest(cacheAddressArrayDevice, 0x6000_0000, 0x0000_0000); // Address bit 2 clear invalidates the selected entry.
    AssertEqual(0x0000_0000u, ReadSh2LongForTest(cacheAddressArrayDevice, 0x6000_0000) & 0x0008_0002u);

    byte[] twoWayCacheRom = new byte[0x80];
    twoWayCacheRom[0x00] = 0x12;
    twoWayCacheRom[0x01] = 0x34;
    ThirtyTwoXDevice twoWayCacheDevice = new(twoWayCacheRom);
    WriteSh2ByteForTest(twoWayCacheDevice, 0xFFFF_FE92, 0x09);
    WriteSh2ByteForTest(twoWayCacheDevice, 0xC000_0000, 0xAA);
    WriteSh2ByteForTest(twoWayCacheDevice, 0xC000_0400, 0xBB);
    AssertEqual((byte)0x12, ReadSh2ByteForTest(twoWayCacheDevice, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedCachedStart));
    AssertEqual((byte)0xAA, ReadSh2ByteForTest(twoWayCacheDevice, 0xC000_0000));
    AssertEqual((byte)0xBB, ReadSh2ByteForTest(twoWayCacheDevice, 0xC000_0400));

    byte[] taggedLongRom = new byte[0x80];
    taggedLongRom[0x20] = 0x11;
    taggedLongRom[0x21] = 0x22;
    taggedLongRom[0x22] = 0x33;
    taggedLongRom[0x23] = 0x44;
    WriteWord(taggedLongRom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(taggedLongRom, 0x02, 0x5102); // MOV.L @(8,R0),R1
    WriteWord(taggedLongRom, 0x04, 0x001B); // SLEEP
    WriteLong(taggedLongRom, 0x14, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedCachedStart + 0x19);
    ThirtyTwoXDevice taggedLongDevice = new(taggedLongRom);
    WriteSh2ByteForTest(taggedLongDevice, 0xFFFF_FE92, 0x01);
    taggedLongDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    taggedLongDevice.MasterSh2.Run(8);
    AssertEqual(0x1122_3344u, taggedLongDevice.MasterSh2.R[1]);

    byte[] internalRegisterRom = new byte[0x100];
    WriteWord(internalRegisterRom, 0x00, 0xD003); // MOV.L @(literal,PC),R0
    WriteWord(internalRegisterRom, 0x02, 0x6201); // MOV.W @R0,R2
    WriteWord(internalRegisterRom, 0x04, 0x001B); // SLEEP
    WriteLong(internalRegisterRom, 0x10, 0xFFFF_FF40);
    ThirtyTwoXDevice internalRegisterDevice = new(internalRegisterRom);
    internalRegisterDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    internalRegisterDevice.MasterSh2.Run(8);
    AssertEqual(0x0000_0000u, internalRegisterDevice.MasterSh2.R[2]);

    ThirtyTwoXDevice.ThirtyTwoXState state = device.CaptureState();
    device.MasterSh2.Reset();
    device.RestoreState(state);
    AssertEqual(0x0000_005Au, device.MasterSh2.R[2]);
    AssertEqual((byte)0x5A, device.Sdram[3]);

    byte[] branchRom = new byte[0x100];
    WriteWord(branchRom, 0x00, 0xE001); // MOV #1,R0
    WriteWord(branchRom, 0x02, 0xE105); // MOV #5,R1
    WriteWord(branchRom, 0x04, 0x3010); // CMP/EQ R1,R0
    WriteWord(branchRom, 0x06, 0x8F01); // BF/S to $0C
    WriteWord(branchRom, 0x08, 0xE2FF); // MOV #-1,R2 in delay slot
    WriteWord(branchRom, 0x0A, 0xE222); // skipped
    WriteWord(branchRom, 0x0C, 0xE333); // MOV #$33,R3
    WriteWord(branchRom, 0x0E, 0x001B); // SLEEP
    ThirtyTwoXDevice branchDevice = new(branchRom);
    branchDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    branchDevice.MasterSh2.Run(16);
    AssertEqual(0xFFFF_FFFFu, branchDevice.MasterSh2.R[2]);
    AssertEqual(0x0000_0033u, branchDevice.MasterSh2.R[3]);
    AssertTrue(branchDevice.MasterSh2.Halted, "SH-2 branch test should halt");

    byte[] notRom = new byte[0x20];
    WriteWord(notRom, 0x00, 0xE1F0); // MOV #-$10,R1
    WriteWord(notRom, 0x02, 0x6217); // NOT R1,R2
    WriteWord(notRom, 0x04, 0x611B); // NEG R1,R1
    WriteWord(notRom, 0x06, 0x001B); // SLEEP
    ThirtyTwoXDevice notDevice = new(notRom);
    notDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    notDevice.MasterSh2.Run(8);
    AssertEqual(0x0000_000Fu, notDevice.MasterSh2.R[2]);
    AssertEqual(0x0000_0010u, notDevice.MasterSh2.R[1]);

    byte[] shiftAndTasRom = new byte[0x100];
    WriteWord(shiftAndTasRom, 0x00, 0xE5FC); // MOV #-4,R5
    WriteWord(shiftAndTasRom, 0x02, 0xE2FF); // MOV #-1,R2
    WriteWord(shiftAndTasRom, 0x04, 0x452C); // SHAD R2,R5
    WriteWord(shiftAndTasRom, 0x06, 0xE301); // MOV #1,R3
    WriteWord(shiftAndTasRom, 0x08, 0xE404); // MOV #4,R4
    WriteWord(shiftAndTasRom, 0x0A, 0x434D); // SHLD R4,R3
    WriteWord(shiftAndTasRom, 0x0C, 0xE4FF); // MOV #-1,R4
    WriteWord(shiftAndTasRom, 0x0E, 0x434D); // SHLD R4,R3
    WriteWord(shiftAndTasRom, 0x10, 0xD103); // MOV.L @(literal,PC),R1
    WriteWord(shiftAndTasRom, 0x12, 0x411B); // TAS.B @R1
    WriteWord(shiftAndTasRom, 0x14, 0x001B); // SLEEP
    WriteLong(shiftAndTasRom, 0x20, ThirtyTwoXHardwareProfile.Sh2SdramStart);
    ThirtyTwoXDevice shiftAndTasDevice = new(shiftAndTasRom);
    shiftAndTasDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    shiftAndTasDevice.MasterSh2.Run(16);
    AssertEqual(0xFFFF_FFFEu, shiftAndTasDevice.MasterSh2.R[5]);
    AssertEqual(0x0000_0008u, shiftAndTasDevice.MasterSh2.R[3]);
    AssertTrue((shiftAndTasDevice.MasterSh2.SR & 1u) != 0, "TAS.B should set T when the tested byte is zero");
    AssertEqual((byte)0x80, shiftAndTasDevice.Sdram[0]);

    byte[] macRom = new byte[0x100];
    WriteWord(macRom, 0x00, 0xD105); // MOV.L @(literal,PC),R1
    WriteWord(macRom, 0x02, 0xD206); // MOV.L @(literal,PC),R2
    WriteWord(macRom, 0x04, 0x412F); // MAC.W @R2+,@R1+
    WriteWord(macRom, 0x06, 0xD407); // MOV.L @(literal,PC),R4
    WriteWord(macRom, 0x08, 0xD308); // MOV.L @(literal,PC),R3
    WriteWord(macRom, 0x0A, 0x043F); // MAC.L @R3+,@R4+
    WriteWord(macRom, 0x0C, 0x001B); // SLEEP
    WriteLong(macRom, 0x18, ThirtyTwoXHardwareProfile.Sh2SdramStart + 2);
    WriteLong(macRom, 0x1C, ThirtyTwoXHardwareProfile.Sh2SdramStart + 4);
    WriteLong(macRom, 0x24, ThirtyTwoXHardwareProfile.Sh2SdramStart);
    WriteLong(macRom, 0x2C, ThirtyTwoXHardwareProfile.Sh2SdramStart + 8);
    ThirtyTwoXDevice macDevice = new(macRom);
    macDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    ThirtyTwoXDevice.ThirtyTwoXState macState = macDevice.CaptureState();
    macState.Sdram[3] = 2;
    macState.Sdram[5] = 3;
    macState.Sdram[11] = 4;
    macDevice.RestoreState(macState);
    macDevice.MasterSh2.Run(16);
    AssertEqual(0x0000_000Eu, macDevice.MasterSh2.MACL);
    AssertEqual(0x0000_0000u, macDevice.MasterSh2.MACH);
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2SdramStart + 4, macDevice.MasterSh2.R[1]);
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2SdramStart + 6, macDevice.MasterSh2.R[2]);
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2SdramStart + 4, macDevice.MasterSh2.R[4]);
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2SdramStart + 12, macDevice.MasterSh2.R[3]);

    byte[] trapRom = new byte[0x100];
    WriteWord(trapRom, 0x00, 0xC301); // TRAPA #1
    WriteWord(trapRom, 0x02, 0xE111); // MOV #$11,R1 after RTE
    WriteWord(trapRom, 0x04, 0x001B); // SLEEP
    WriteWord(trapRom, 0x10, 0x002B); // RTE
    WriteWord(trapRom, 0x12, 0xE222); // MOV #$22,R2 in RTE delay slot
    WriteLong(trapRom, 0x44, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x10);
    ThirtyTwoXDevice trapDevice = new(trapRom);
    trapDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    trapDevice.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x40);
    trapDevice.MasterSh2.R[15] = ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x100;
    trapDevice.MasterSh2.Run(16);
    AssertEqual(0x0000_0011u, trapDevice.MasterSh2.R[1]);
    AssertEqual(0x0000_0022u, trapDevice.MasterSh2.R[2]);
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x100, trapDevice.MasterSh2.R[15]);
    AssertTrue(trapDevice.MasterSh2.Halted, "SH-2 trap/RTE program should return and halt");

    byte[] interruptRom = new byte[0x200];
    WriteWord(interruptRom, 0x00, 0xE002); // MOV #2,R0
    WriteWord(interruptRom, 0x02, 0x400E); // LDC R0,SR, leaving level-8 command interrupts unmasked
    WriteWord(interruptRom, 0x04, 0xAFFE); // BRA *
    WriteWord(interruptRom, 0x06, 0x0009); // NOP
    WriteWord(interruptRom, 0x80, 0xE155); // MOV #$55,R1
    WriteWord(interruptRom, 0x82, 0x001B); // SLEEP
    WriteLong(interruptRom, 0x40 + (65 * 4), ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x80);
    WriteLong(interruptRom, 0x40 + (72 * 4), ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x90);
    WriteWord(interruptRom, 0x90, 0xE166); // Wrong vector if level is used as the vector index.
    WriteWord(interruptRom, 0x92, 0x001B);
    ThirtyTwoXDevice interruptDevice = new(interruptRom);
    interruptDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    interruptDevice.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x40);
    interruptDevice.MasterSh2.Run(3);
    interruptDevice.MasterSh2.RequestInterrupt(8, 65);
    interruptDevice.MasterSh2.Run(8);
    AssertEqual(0x0000_0055u, interruptDevice.MasterSh2.R[1]);
    AssertTrue(interruptDevice.MasterSh2.Halted, "32X command interrupts should use the supplied autovector number, not 64 + level");

    byte[] srMaskRom = new byte[0x40];
    WriteWord(srMaskRom, 0x00, 0xE0F1); // MOV #$F1,R0 sign-extends to $FFFFFFF1.
    WriteWord(srMaskRom, 0x02, 0x400E); // LDC R0,SR
    WriteWord(srMaskRom, 0x04, 0x0102); // STC SR,R1
    WriteWord(srMaskRom, 0x06, 0x001B); // SLEEP
    ThirtyTwoXDevice srMaskDevice = new(srMaskRom);
    srMaskDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    srMaskDevice.MasterSh2.Run(8);
    AssertEqual(0x0000_03F1u, srMaskDevice.MasterSh2.R[1]);
    AssertEqual(0x0000_03F1u, srMaskDevice.MasterSh2.SR);

    byte[] delaySlotPcRelativeRom = new byte[0x40];
    WriteWord(delaySlotPcRelativeRom, 0x00, 0xA002); // BRA $08
    WriteWord(delaySlotPcRelativeRom, 0x02, 0x9100); // MOV.W @(0,PC),R1 in the delay slot
    WriteWord(delaySlotPcRelativeRom, 0x04, 0x5678); // Would be read if the slot used branch PC + 4.
    WriteWord(delaySlotPcRelativeRom, 0x06, 0x9ABC); // Would be read if the slot used its own PC + 4.
    WriteWord(delaySlotPcRelativeRom, 0x08, 0x001B); // SLEEP
    WriteWord(delaySlotPcRelativeRom, 0x0A, 0x1234); // Correct source: branch destination + 2.
    ThirtyTwoXDevice delaySlotPcRelativeDevice = new(delaySlotPcRelativeRom);
    delaySlotPcRelativeDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    delaySlotPcRelativeDevice.MasterSh2.Run(8);
    AssertEqual(0x0000_1234u, delaySlotPcRelativeDevice.MasterSh2.R[1]);

    byte[] sleepWakeRom = new byte[0x200];
    WriteWord(sleepWakeRom, 0x00, 0xE002); // MOV #2,R0
    WriteWord(sleepWakeRom, 0x02, 0x400E); // LDC R0,SR
    WriteWord(sleepWakeRom, 0x04, 0x001B); // SLEEP
    WriteWord(sleepWakeRom, 0x80, 0xE377); // MOV #$77,R3
    WriteWord(sleepWakeRom, 0x82, 0x001B); // SLEEP
    WriteLong(sleepWakeRom, 0x40 + (65 * 4), ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x80);
    ThirtyTwoXDevice sleepWakeDevice = new(sleepWakeRom);
    sleepWakeDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    sleepWakeDevice.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x40);
    sleepWakeDevice.MasterSh2.Run(3);
    AssertTrue(sleepWakeDevice.MasterSh2.Halted, "SH-2 SLEEP should halt until an interrupt is accepted");
    sleepWakeDevice.MasterSh2.RequestInterrupt(8, 65);
    sleepWakeDevice.MasterSh2.Run(4);
    AssertEqual(0x0000_0077u, sleepWakeDevice.MasterSh2.R[3]);
    AssertTrue(sleepWakeDevice.MasterSh2.Halted, "SH-2 should wake from SLEEP for an unmasked interrupt and run the handler");

    ThirtyTwoXDevice clearedInterruptDevice = new(interruptRom);
    clearedInterruptDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    clearedInterruptDevice.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x40);
    clearedInterruptDevice.MasterSh2.Run(3);
    clearedInterruptDevice.MasterSh2.RequestInterrupt(8, 65);
    clearedInterruptDevice.MasterSh2.ClearPendingInterrupt(8, 65);
    clearedInterruptDevice.MasterSh2.Run(8);
    AssertEqual(0x0000_0000u, clearedInterruptDevice.MasterSh2.R[1]);
    AssertTrue(!clearedInterruptDevice.MasterSh2.Halted, "cleared pending SH-2 interrupts should not fire after SR unmasks them");

    byte[] illegalRom = new byte[0x100];
    WriteWord(illegalRom, 0x00, 0xFFFF); // undefined on SH-2
    WriteWord(illegalRom, 0x02, 0xE155); // MOV #$55,R1 after illegal handler returns
    WriteWord(illegalRom, 0x04, 0x001B); // SLEEP
    WriteWord(illegalRom, 0x20, 0x002B); // RTE
    WriteWord(illegalRom, 0x22, 0xE266); // MOV #$66,R2 in RTE delay slot
    WriteLong(illegalRom, 0x50, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x20);
    ThirtyTwoXDevice illegalDevice = new(illegalRom);
    illegalDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    illegalDevice.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x40);
    illegalDevice.MasterSh2.R[15] = ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x100;
    illegalDevice.MasterSh2.Run(16);
    AssertEqual(0x0000_0055u, illegalDevice.MasterSh2.R[1]);
    AssertEqual(0x0000_0066u, illegalDevice.MasterSh2.R[2]);
    AssertTrue(illegalDevice.MasterSh2.Halted, "SH-2 illegal opcode should vector through the general illegal handler");
}

void ThirtyTwoXSh2BraSelfIdleLoopFastForward()
{
    const uint LoopPc = 0x0600_01A8;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xAFFE); // BRA *
    bus.WriteInstructionWord(LoopPc + 2, 0x0009); // NOP

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    AssertTrue(cpu.TryFastForwardBraSelfNopIdleLoop(101, out int cycles), "BRA self idle loop should fast-forward");
    AssertEqual(100, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(100L, cpu.Cycles);
    AssertEqual((ushort)0x0009, cpu.LastOpcode);
    AssertEqual(LoopPc + 2, cpu.LastOpcodePc);

    Sh2Cpu interrupted = new(bus, "test");
    interrupted.Reset(LoopPc);
    SetSh2Property(interrupted, nameof(Sh2Cpu.SR), 0u);
    interrupted.RequestInterrupt(15, 65);
    AssertTrue(!interrupted.TryFastForwardBraSelfNopIdleLoop(100, out _), "pending acceptable interrupt should fall back to the interpreter");

    SyntheticSh2Bus nonIdleBus = new();
    nonIdleBus.WriteInstructionWord(LoopPc + 0, 0xAFFE);
    nonIdleBus.WriteInstructionWord(LoopPc + 2, 0x000B); // RTS, not NOP.
    Sh2Cpu nonIdle = new(nonIdleBus, "test");
    nonIdle.Reset(LoopPc);
    AssertTrue(!nonIdle.TryFastForwardBraSelfNopIdleLoop(100, out _), "non-NOP delay slot should not fast-forward");

    const uint NopBranchLoopPc = 0x0600_0300;
    SyntheticSh2Bus nopBranchBus = new();
    nopBranchBus.WriteInstructionWord(NopBranchLoopPc + 0, 0x0009); // NOP
    nopBranchBus.WriteInstructionWord(NopBranchLoopPc + 2, 0xAFFD); // BRA back to first NOP
    nopBranchBus.WriteInstructionWord(NopBranchLoopPc + 4, 0x0009); // NOP delay slot
    Sh2Cpu nopBranch = new(nopBranchBus, "test");
    nopBranch.Reset(NopBranchLoopPc);
    AssertTrue(nopBranch.TryFastForwardBraSelfNopIdleLoop(101, out int nopBranchCycles), "NOP/BRA/NOP idle loop should fast-forward from the leading NOP");
    AssertEqual(99, nopBranchCycles);
    AssertEqual(NopBranchLoopPc, nopBranch.PC);

    nopBranch.Reset(NopBranchLoopPc + 2);
    AssertTrue(nopBranch.TryFastForwardBraSelfNopIdleLoop(101, out int branchEntryCycles), "NOP/BRA/NOP idle loop should fast-forward from the branch");
    AssertEqual(101, branchEntryCycles);
    AssertEqual(NopBranchLoopPc, nopBranch.PC);

    const uint TwoNopBranchLoopPc = 0x0600_0400;
    SyntheticSh2Bus twoNopBranchBus = new();
    twoNopBranchBus.WriteInstructionWord(TwoNopBranchLoopPc + 0, 0x0009); // NOP
    twoNopBranchBus.WriteInstructionWord(TwoNopBranchLoopPc + 2, 0x0009); // NOP
    twoNopBranchBus.WriteInstructionWord(TwoNopBranchLoopPc + 4, 0xAFFC); // BRA back to first NOP
    twoNopBranchBus.WriteInstructionWord(TwoNopBranchLoopPc + 6, 0x0009); // NOP delay slot
    Sh2Cpu twoNopBranch = new(twoNopBranchBus, "test");
    twoNopBranch.Reset(TwoNopBranchLoopPc);
    AssertTrue(twoNopBranch.TryFastForwardBraSelfNopIdleLoop(103, out int twoNopCycles), "NOP/NOP/BRA/NOP idle loop should fast-forward from the first NOP");
    AssertEqual(100, twoNopCycles);
    AssertEqual(TwoNopBranchLoopPc, twoNopBranch.PC);

    twoNopBranch.Reset(TwoNopBranchLoopPc + 2);
    AssertTrue(twoNopBranch.TryFastForwardBraSelfNopIdleLoop(103, out int secondNopCycles), "NOP/NOP/BRA/NOP idle loop should fast-forward from the second NOP");
    AssertEqual(101, secondNopCycles);
    AssertEqual(TwoNopBranchLoopPc, twoNopBranch.PC);

    twoNopBranch.Reset(TwoNopBranchLoopPc + 4);
    AssertTrue(twoNopBranch.TryFastForwardBraSelfNopIdleLoop(103, out int twoNopBranchCycles), "NOP/NOP/BRA/NOP idle loop should fast-forward from the branch");
    AssertEqual(103, twoNopBranchCycles);
    AssertEqual(TwoNopBranchLoopPc, twoNopBranch.PC);
}

void ThirtyTwoXSh2AddBraNopDelayLoopFastForward()
{
    const uint LoopPc = 0x0600_034E;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x7001); // ADD #1,R0
    bus.WriteInstructionWord(LoopPc + 2, 0xAFFD); // BRA back to ADD
    bus.WriteInstructionWord(LoopPc + 4, 0x0009); // NOP delay slot

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    AssertTrue(cpu.TryFastForwardAddBraNopDelayLoop(401, out int cycles), "ADD/BRA/NOP delay loop should fast-forward from the ADD");
    AssertEqual(400, cycles);
    AssertEqual(100u, cpu.R[0]);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(400L, cpu.Cycles);
    AssertEqual((ushort)0xAFFD, cpu.LastOpcode);
    AssertEqual(LoopPc + 2, cpu.LastOpcodePc);

    cpu.Reset(LoopPc + 2);
    cpu.R[0] = 5;
    AssertTrue(cpu.TryFastForwardAddBraNopDelayLoop(40, out int branchEntryCycles), "ADD/BRA/NOP delay loop should fast-forward from the branch");
    AssertEqual(40, branchEntryCycles);
    AssertEqual(15u, cpu.R[0]);
    AssertEqual(LoopPc, cpu.PC);

    cpu.Reset(LoopPc + 4);
    cpu.R[0] = 10;
    AssertTrue(cpu.TryFastForwardAddBraNopDelayLoop(40, out int delayEntryCycles), "ADD/BRA/NOP delay loop should fast-forward from the delay slot");
    AssertEqual(40, delayEntryCycles);
    AssertEqual(20u, cpu.R[0]);
    AssertEqual(LoopPc, cpu.PC);

    const uint DecrementLoopPc = 0x0600_0410;
    SyntheticSh2Bus decrementBus = new();
    decrementBus.WriteInstructionWord(DecrementLoopPc + 0, 0x72FF); // ADD #-1,R2
    decrementBus.WriteInstructionWord(DecrementLoopPc + 2, 0xAFFD);
    decrementBus.WriteInstructionWord(DecrementLoopPc + 4, 0x0009);
    Sh2Cpu decrement = new(decrementBus, "test");
    decrement.Reset(DecrementLoopPc);
    decrement.R[2] = 50;
    AssertTrue(decrement.TryFastForwardAddBraNopDelayLoop(80, out int decrementCycles), "negative immediate ADD/BRA/NOP delay loop should fast-forward");
    AssertEqual(80, decrementCycles);
    AssertEqual(30u, decrement.R[2]);

    SyntheticSh2Bus nonIdleBus = new();
    nonIdleBus.WriteInstructionWord(LoopPc + 0, 0x7001);
    nonIdleBus.WriteInstructionWord(LoopPc + 2, 0xAFFD);
    nonIdleBus.WriteInstructionWord(LoopPc + 4, 0x000B); // RTS, not NOP.
    Sh2Cpu nonIdle = new(nonIdleBus, "test");
    nonIdle.Reset(LoopPc);
    AssertTrue(!nonIdle.TryFastForwardAddBraNopDelayLoop(40, out _), "non-NOP delay slot should not fast-forward");
}

void ThirtyTwoXSh2DtBfDelayLoopFastForward()
{
    byte[] rom = new byte[0x100];
    WriteWord(rom, 0x00, 0x4010); // DT R0
    WriteWord(rom, 0x02, 0x8BFD); // BF $-6 back to DT
    WriteWord(rom, 0x04, 0x001B); // SLEEP

    ThirtyTwoXDevice device = new(rom);
    device.Reset();
    device.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    device.RestoreState(device.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
    });
    device.MasterSh2.R[0] = 100;

    int partial = device.RunSh2Cycles(20);
    AssertTrue(partial <= 2, "partial DT/BF loop should collapse to at most one scheduler step per SH-2");
    AssertEqual(90u, device.MasterSh2.R[0]);
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart, device.MasterSh2.PC);
    AssertTrue(!device.MasterSh2.Halted, "partial DT/BF fast-forward should remain in the loop");

    int completed = device.RunSh2Cycles(200);
    AssertTrue(completed <= 4, "completed DT/BF fast-forward should collapse the remaining loop");
    AssertEqual(0u, device.MasterSh2.R[0]);
    AssertTrue(device.MasterSh2.Halted, "SH-2 should execute the instruction after the collapsed delay loop");

    ThirtyTwoXDevice branchEntryDevice = new(rom);
    branchEntryDevice.Reset();
    branchEntryDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    branchEntryDevice.RestoreState(branchEntryDevice.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
    });
    branchEntryDevice.MasterSh2.R[0] = 32;
    SetSh2BoolProperty(branchEntryDevice.SlaveSh2, nameof(Sh2Cpu.Halted), true);
    branchEntryDevice.RunSh2Cycles(1);
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 2, branchEntryDevice.MasterSh2.PC);
    int branchEntryPartial = branchEntryDevice.RunSh2Cycles(21);
    AssertTrue(branchEntryPartial <= 3, "branch-entry DT/BF loop should collapse after landing on BF");
    AssertEqual(27u, branchEntryDevice.MasterSh2.R[0]);
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart, branchEntryDevice.MasterSh2.PC);
}

void ThirtyTwoXSh2NopDtBfDelayLoopFastForward()
{
    byte[] rom = new byte[0x100];
    WriteWord(rom, 0x00, 0x0009); // NOP
    WriteWord(rom, 0x02, 0x0009); // NOP
    WriteWord(rom, 0x04, 0x0009); // NOP
    WriteWord(rom, 0x06, 0x4010); // DT R0
    WriteWord(rom, 0x08, 0x8BFA); // BF $-12 back to first NOP
    WriteWord(rom, 0x0A, 0x001B); // SLEEP

    ThirtyTwoXDevice device = new(rom);
    device.Reset();
    device.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    device.RestoreState(device.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
    });
    device.MasterSh2.R[0] = 50;

    int partial = device.RunSh2Cycles(25);
    AssertTrue(partial <= 2, "partial NOP/DT/BF loop should collapse to at most one scheduler step per SH-2");
    AssertEqual(45u, device.MasterSh2.R[0]);
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart, device.MasterSh2.PC);
    AssertTrue(!device.MasterSh2.Halted, "partial NOP/DT/BF fast-forward should remain in the loop");

    int completed = device.RunSh2Cycles(250);
    AssertTrue(completed <= 4, "completed NOP/DT/BF fast-forward should collapse the remaining loop");
    AssertEqual(0u, device.MasterSh2.R[0]);
    AssertTrue(device.MasterSh2.Halted, "SH-2 should execute the instruction after the collapsed NOP delay loop");
}

void ThirtyTwoXSh2MovLAddBfSDtLoopFastForward()
{
    const uint LoopPc = 0x0600_0000;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x2102); // MOV.L R0,@R1
    bus.WriteInstructionWord(LoopPc + 2, 0x7104); // ADD #4,R1
    bus.WriteInstructionWord(LoopPc + 4, 0x8FFC); // BF/S loop
    bus.WriteInstructionWord(LoopPc + 6, 0x4210); // DT R2

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[0] = 0x1234_5678;
    cpu.R[1] = 0x0603_0000;
    cpu.R[2] = 5;
    AssertTrue(cpu.TryFastForwardMovLStoreAddBfSDtLoop(12, bus.TryWriteLong, 4, out int partialCycles), "partial MOV.L/ADD/BF/S/DT loop should fast-forward");
    AssertEqual(12, partialCycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x0603_000Cu, cpu.R[1]);
    AssertEqual(2u, cpu.R[2]);
    AssertEqual(0u, cpu.SR & 1);
    AssertEqual(0x1234_5678u, bus.ReadLong(0x0603_0000));
    AssertEqual(0x1234_5678u, bus.ReadLong(0x0603_0008));

    AssertTrue(cpu.TryFastForwardMovLStoreAddBfSDtLoop(16, bus.TryWriteLong, 4, out int finalCycles), "final MOV.L/ADD/BF/S/DT loop should fast-forward through exit");
    AssertEqual(8, finalCycles);
    AssertEqual(LoopPc + 8, cpu.PC);
    AssertEqual(0x0603_0014u, cpu.R[1]);
    AssertEqual(0u, cpu.R[2]);
    AssertEqual(1u, cpu.SR & 1);
    AssertEqual(0x1234_5678u, bus.ReadLong(0x0603_0010));

    Sh2Cpu rejected = new(bus, "test");
    rejected.Reset(LoopPc);
    rejected.R[1] = 0xFFFF_0000;
    rejected.R[2] = 1;
    AssertTrue(!rejected.TryFastForwardMovLStoreAddBfSDtLoop(4, (address, value) => false, 4, out _), "loop should fall back when the writer rejects the destination");
}

void ThirtyTwoXSh2MovLNopDtBfSAddLoopFastForward()
{
    const uint LoopPc = 0x0600_1000;
    const uint Destination = 0x2400_0200;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x2202); // MOV.L R0,@R2
    bus.WriteInstructionWord(LoopPc + 2, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 4, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 6, 0x4110); // DT R1
    bus.WriteInstructionWord(LoopPc + 8, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 10, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 12, 0x8FF8); // BF/S loop
    bus.WriteInstructionWord(LoopPc + 14, 0x7204); // ADD #4,R2
    bus.WriteInstructionWord(LoopPc + 16, 0x001B); // SLEEP

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[0] = 0xA5A5_5A5A;
    cpu.R[1] = 8;
    cpu.R[2] = Destination;

    AssertTrue(cpu.TryFastForwardMovLNopDtBfSAddLoop(80, bus.TryWriteLong, 10, out int partialCycles), "partial MOV.L/NOP/DT/BF/S/ADD loop should fast-forward");
    AssertEqual(80, partialCycles);
    AssertEqual(0u, cpu.R[1]);
    AssertEqual(Destination + 32, cpu.R[2]);
    AssertEqual(LoopPc + 16, cpu.PC);
    for (uint i = 0; i < 8; i++)
    {
        AssertEqual(0xA5A5_5A5Au, bus.ReadLong(Destination + (i * 4)));
    }

    Sh2Cpu partialCpu = new(bus, "test-partial");
    partialCpu.Reset(LoopPc);
    partialCpu.R[0] = 0x1122_3344;
    partialCpu.R[1] = 20;
    partialCpu.R[2] = Destination + 0x100;

    AssertTrue(partialCpu.TryFastForwardMovLNopDtBfSAddLoop(50, bus.TryWriteLong, 10, out int chunkCycles), "bounded MOV.L/NOP/DT/BF/S/ADD loop should stop on budget");
    AssertEqual(50, chunkCycles);
    AssertEqual(15u, partialCpu.R[1]);
    AssertEqual(Destination + 0x114, partialCpu.R[2]);
    AssertEqual(LoopPc, partialCpu.PC);
    AssertEqual(0x1122_3344u, bus.ReadLong(Destination + 0x110));
}

void ThirtyTwoXSh2MovWStoreDtBfSAddLoopFastForward()
{
    const uint StoreFirstLoopPc = 0x0600_0814;
    const uint StoreFirstDestination = 0x2000_4200;
    SyntheticSh2Bus storeFirstBus = new();
    storeFirstBus.WriteInstructionWord(StoreFirstLoopPc + 0, 0x2101); // MOV.W R0,@R1
    storeFirstBus.WriteInstructionWord(StoreFirstLoopPc + 2, 0x4210); // DT R2
    storeFirstBus.WriteInstructionWord(StoreFirstLoopPc + 4, 0x8FFC); // BF/S loop
    storeFirstBus.WriteInstructionWord(StoreFirstLoopPc + 6, 0x7102); // ADD #2,R1

    Sh2Cpu storeFirst = new(storeFirstBus, "test");
    storeFirst.Reset(StoreFirstLoopPc + 4);
    storeFirst.R[0] = 0x0000_1357;
    storeFirst.R[1] = StoreFirstDestination;
    storeFirst.R[2] = 3;
    AssertTrue(
        storeFirst.TryFastForwardMovWStoreDtBfSAddLoop(18, storeFirstBus.TryWriteWord, 6, out int storeFirstCycles),
        "store/DT/BF/S/add word fill loop should fast-forward from branch");
    AssertEqual(18, storeFirstCycles);
    AssertEqual(StoreFirstLoopPc + 8, storeFirst.PC);
    AssertEqual(StoreFirstDestination + 6, storeFirst.R[1]);
    AssertEqual(0u, storeFirst.R[2]);
    AssertEqual(1u, storeFirst.SR & 1);
    AssertEqual(0x1357, storeFirstBus.ReadWord(StoreFirstDestination + 0));
    AssertEqual(0x1357, storeFirstBus.ReadWord(StoreFirstDestination + 2));
    AssertEqual(0x1357, storeFirstBus.ReadWord(StoreFirstDestination + 4));

    const uint DtFirstLoopPc = 0x0600_0E3A;
    const uint DtFirstDestination = 0x0600_8000;
    SyntheticSh2Bus dtFirstBus = new();
    dtFirstBus.WriteInstructionWord(DtFirstLoopPc + 0, 0x4310); // DT R3
    dtFirstBus.WriteInstructionWord(DtFirstLoopPc + 2, 0x2201); // MOV.W R0,@R2
    dtFirstBus.WriteInstructionWord(DtFirstLoopPc + 4, 0x8FFC); // BF/S loop
    dtFirstBus.WriteInstructionWord(DtFirstLoopPc + 6, 0x7202); // ADD #2,R2

    Sh2Cpu dtFirst = new(dtFirstBus, "test");
    dtFirst.Reset(DtFirstLoopPc);
    dtFirst.R[0] = 0x0000_2468;
    dtFirst.R[2] = DtFirstDestination;
    dtFirst.R[3] = 4;
    AssertTrue(
        dtFirst.TryFastForwardMovWStoreDtBfSAddLoop(12, dtFirstBus.TryWriteWord, 6, out int partialCycles),
        "DT/store/BF/S/add word fill loop should stop on budget");
    AssertEqual(12, partialCycles);
    AssertEqual(DtFirstLoopPc, dtFirst.PC);
    AssertEqual(DtFirstDestination + 4, dtFirst.R[2]);
    AssertEqual(2u, dtFirst.R[3]);
    AssertEqual(0u, dtFirst.SR & 1);
    AssertEqual(0x2468, dtFirstBus.ReadWord(DtFirstDestination + 0));
    AssertEqual(0x2468, dtFirstBus.ReadWord(DtFirstDestination + 2));

    AssertTrue(
        dtFirst.TryFastForwardMovWStoreDtBfSAddLoop(12, dtFirstBus.TryWriteWord, 6, out int finalCycles),
        "DT/store/BF/S/add word fill loop should finish on the next burst");
    AssertEqual(12, finalCycles);
    AssertEqual(DtFirstLoopPc + 8, dtFirst.PC);
    AssertEqual(DtFirstDestination + 8, dtFirst.R[2]);
    AssertEqual(0u, dtFirst.R[3]);
    AssertEqual(1u, dtFirst.SR & 1);
    AssertEqual(0x2468, dtFirstBus.ReadWord(DtFirstDestination + 4));
    AssertEqual(0x2468, dtFirstBus.ReadWord(DtFirstDestination + 6));
}

void ThirtyTwoXSh2MovWStoreAddRegisterDtBfLoopFastForward()
{
    const uint LoopPc = 0x0600_0DB4;
    const uint Destination = 0x2400_0000;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x2861); // MOV.W R6,@R8
    bus.WriteInstructionWord(LoopPc + 2, 0x7802); // ADD #2,R8
    bus.WriteInstructionWord(LoopPc + 4, 0x365C); // ADD R5,R6
    bus.WriteInstructionWord(LoopPc + 6, 0x4710); // DT R7
    bus.WriteInstructionWord(LoopPc + 8, 0x8BFA); // BF loop

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc + 8);
    cpu.R[5] = 0x0000_00A4;
    cpu.R[6] = 0x0000_1580;
    cpu.R[7] = 4;
    cpu.R[8] = Destination;
    AssertTrue(
        cpu.TryFastForwardMovWStoreAddRegisterDtBfLoop(28, bus.TryWriteWord, 7, out int cycles),
        "MOV.W/add/register-add/DT/BF ramp loop should fast-forward from branch");
    AssertEqual(28, cycles);
    AssertEqual(LoopPc + 10, cpu.PC);
    AssertEqual(Destination + 8, cpu.R[8]);
    AssertEqual(0x0000_1810u, cpu.R[6]);
    AssertEqual(0u, cpu.R[7]);
    AssertEqual(1u, cpu.SR & 1);
    AssertEqual(0x1580, bus.ReadWord(Destination + 0));
    AssertEqual(0x1624, bus.ReadWord(Destination + 2));
    AssertEqual(0x16C8, bus.ReadWord(Destination + 4));
    AssertEqual(0x176C, bus.ReadWord(Destination + 6));

    Sh2Cpu partial = new(bus, "test-partial");
    partial.Reset(LoopPc + 4);
    partial.R[5] = 2;
    partial.R[6] = 0x1000;
    partial.R[7] = 5;
    partial.R[8] = Destination + 0x100;
    AssertTrue(
        partial.TryFastForwardMovWStoreAddRegisterDtBfLoop(14, bus.TryWriteWord, 7, out int partialCycles),
        "MOV.W/add/register-add/DT/BF ramp loop should stop on budget");
    AssertEqual(14, partialCycles);
    AssertEqual(LoopPc, partial.PC);
    AssertEqual(Destination + 0x104, partial.R[8]);
    AssertEqual(0x1004u, partial.R[6]);
    AssertEqual(3u, partial.R[7]);
    AssertEqual(0u, partial.SR & 1);
}

void ThirtyTwoXSh2WordTableSearchLoopFastForward()
{
    const uint LoopPc = 0x0600_2000;
    const uint Table = 0x0603_0000;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x613F); // EXTS.W R3,R1
    bus.WriteInstructionWord(LoopPc + 2, 0x6013); // MOV R1,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x301C); // ADD R1,R0
    bus.WriteInstructionWord(LoopPc + 6, 0x017D); // MOV.W @(R0,R7),R1
    bus.WriteInstructionWord(LoopPc + 8, 0x611D); // EXTU.W R1,R1
    bus.WriteInstructionWord(LoopPc + 10, 0x3140); // CMP/EQ R4,R1
    bus.WriteInstructionWord(LoopPc + 12, 0x8903); // BT exit
    bus.WriteInstructionWord(LoopPc + 14, 0x7301); // ADD #1,R3
    bus.WriteInstructionWord(LoopPc + 16, 0x613F); // EXTS.W R3,R1
    bus.WriteInstructionWord(LoopPc + 18, 0x3127); // CMP/GT R2,R1
    bus.WriteInstructionWord(LoopPc + 20, 0x8BF4); // BF loop
    bus.WriteWord(Table + 0, 0xFFFF);
    bus.WriteWord(Table + 2, 0x0007);
    bus.WriteWord(Table + 4, 0x0010);
    bus.WriteWord(Table + 6, 0x0020);

    Sh2Cpu found = new(bus, "test");
    found.Reset(LoopPc);
    found.R[2] = 3;
    found.R[3] = 0;
    found.R[4] = 0x0000_0010;
    found.R[7] = Table;
    AssertTrue(found.TryFastForwardWordTableSearchLoop(128, bus.TryReadWord, out int foundCycles), "word table search should fast-forward to a matching entry");
    AssertEqual(33, foundCycles);
    AssertEqual(LoopPc + 22, found.PC);
    AssertEqual(2u, found.R[3]);
    AssertEqual(4u, found.R[0]);
    AssertEqual(0x0010u, found.R[1]);
    AssertEqual(1u, found.SR & 1);

    Sh2Cpu exhausted = new(bus, "test");
    exhausted.Reset(LoopPc);
    exhausted.R[2] = 3;
    exhausted.R[3] = 0;
    exhausted.R[4] = 0x0000_1234;
    exhausted.R[7] = Table;
    AssertTrue(exhausted.TryFastForwardWordTableSearchLoop(128, bus.TryReadWord, out int exhaustedCycles), "word table search should fast-forward through the not-found exit");
    AssertEqual(44, exhaustedCycles);
    AssertEqual(LoopPc + 22, exhausted.PC);
    AssertEqual(4u, exhausted.R[3]);
    AssertEqual(4u, exhausted.R[1]);
    AssertEqual(6u, exhausted.R[0]);
    AssertEqual(1u, exhausted.SR & 1);

    Sh2Cpu partial = new(bus, "test");
    partial.Reset(LoopPc);
    partial.R[2] = 3;
    partial.R[3] = 0;
    partial.R[4] = 0x0000_1234;
    partial.R[7] = Table;
    AssertTrue(partial.TryFastForwardWordTableSearchLoop(22, bus.TryReadWord, out int partialCycles), "word table search should respect the cycle budget");
    AssertEqual(22, partialCycles);
    AssertEqual(LoopPc, partial.PC);
    AssertEqual(2u, partial.R[3]);
    AssertEqual(2u, partial.R[0]);
    AssertEqual(2u, partial.R[1]);
    AssertEqual(0u, partial.SR & 1);
}

void ThirtyTwoXSh2ByteFillIndexedCmpGeFastForward()
{
    const uint LoopPc = 0x0600_7000;
    const uint Base = 0x0603_3000;

    static void WriteLoop(SyntheticSh2Bus bus, uint loopPc)
    {
        ushort[] opcodes = [0x613F, 0x316C, 0x2170, 0x7301, 0x613F, 0x3123, 0x8BF8, 0x001B];
        for (int i = 0; i < opcodes.Length; i++)
        {
            bus.WriteInstructionWord(loopPc + (uint)(i * 2), opcodes[i]);
        }
    }

    static void SeedState(Sh2Cpu cpu, uint loopPc, uint baseAddress)
    {
        cpu.Reset(loopPc);
        cpu.R[2] = 4;
        cpu.R[3] = 0;
        cpu.R[6] = baseAddress;
        cpu.R[7] = 0x5A;
    }

    SyntheticSh2Bus interpretedBus = new();
    WriteLoop(interpretedBus, LoopPc);
    Sh2Cpu interpreted = new(interpretedBus, "interpreted");
    SeedState(interpreted, LoopPc, Base);
    interpreted.Run(128);
    AssertTrue(interpreted.Halted, "interpreter byte fill loop should reach SLEEP");

    SyntheticSh2Bus fastBus = new();
    WriteLoop(fastBus, LoopPc);
    Sh2Cpu fast = new(fastBus, "fast");
    SeedState(fast, LoopPc, Base);
    AssertTrue(
        fast.TryFastForwardByteFillIndexedCmpGeLoop(64, fastBus.TryWriteByte, out int cycles),
        "byte fill indexed CMP/GE loop should fast-forward");
    AssertEqual(36, cycles);
    fast.Step();
    AssertTrue(fast.Halted, "fast byte fill loop should stop on the following SLEEP");

    for (int i = 0; i < 16; i++)
    {
        AssertEqual(interpreted.R[i], fast.R[i]);
    }

    AssertEqual(interpreted.PC, fast.PC);
    AssertEqual(interpreted.SR, fast.SR);
    for (uint offset = 0; offset < 4; offset++)
    {
        AssertEqual(interpretedBus.ReadByte(Base + offset), fastBus.ReadByte(Base + offset));
    }

    SyntheticSh2Bus partialBus = new();
    WriteLoop(partialBus, LoopPc);
    Sh2Cpu partial = new(partialBus, "partial");
    SeedState(partial, LoopPc, Base);
    AssertTrue(
        partial.TryFastForwardByteFillIndexedCmpGeLoop(18, partialBus.TryWriteByte, out int partialCycles),
        "byte fill indexed CMP/GE loop should respect the cycle budget");
    AssertEqual(18, partialCycles);
    AssertEqual(LoopPc, partial.PC);
    AssertEqual(2u, partial.R[3]);
    AssertEqual(2u, partial.R[1]);
    AssertEqual(0u, partial.SR & 1);
    AssertEqual((byte)0x5A, partialBus.ReadByte(Base));
    AssertEqual((byte)0x5A, partialBus.ReadByte(Base + 1));
    AssertEqual((byte)0x00, partialBus.ReadByte(Base + 2));
}

void ThirtyTwoXSh2WordHighBitMaskTransformFastForward()
{
    const uint LoopPc = 0x0600_3000;
    const uint WordTable = 0x0603_1000;
    const uint ByteTable = 0x0603_2000;

    static void WriteTransformRoutine(SyntheticSh2Bus bus, uint loopPc)
    {
        ushort[] opcodes =
        [
            0x6033, 0x303C, 0x068D, 0xD11C, 0x2169, 0x2118, 0x890A, 0x6173,
            0x4711, 0x8900, 0x7107, 0x4121, 0x4121, 0x4121, 0x319C, 0x6210,
            0x224B, 0x2120, 0x614C, 0x6413, 0x4401, 0x911F, 0x2619, 0x6033,
            0x303C, 0x0865, 0x7301, 0x7501, 0xE107, 0x3517, 0x8BE0, 0x001B
        ];

        for (int i = 0; i < opcodes.Length; i++)
        {
            bus.WriteInstructionWord(loopPc + (uint)(i * 2), opcodes[i]);
        }

        bus.WriteWord(loopPc + 0x6C, 0x7FFF);
        bus.WriteLong(loopPc + 0x78, 0x0000_8000);
    }

    static void SeedTransformState(Sh2Cpu cpu)
    {
        cpu.Reset(LoopPc);
        cpu.R[3] = 0;
        cpu.R[4] = 0x80;
        cpu.R[5] = 0;
        cpu.R[7] = 0;
        cpu.R[8] = WordTable;
        cpu.R[9] = ByteTable;
    }

    static void SeedTransformMemory(SyntheticSh2Bus bus)
    {
        ushort[] words = [0x8001, 0x0002, 0x8003, 0x0004, 0x8005, 0x8006, 0x0007, 0x8008];
        for (uint i = 0; i < words.Length; i++)
        {
            bus.WriteWord(WordTable + (i * 2), words[i]);
        }

        bus.WriteByte(ByteTable, 0x02);
    }

    SyntheticSh2Bus interpretedBus = new();
    WriteTransformRoutine(interpretedBus, LoopPc);
    SeedTransformMemory(interpretedBus);
    Sh2Cpu interpreted = new(interpretedBus, "interpreted");
    SeedTransformState(interpreted);
    interpreted.Run(256);
    AssertTrue(interpreted.Halted, "interpreter transform routine should reach SLEEP");

    SyntheticSh2Bus fastBus = new();
    WriteTransformRoutine(fastBus, LoopPc);
    SeedTransformMemory(fastBus);
    Sh2Cpu fast = new(fastBus, "fast");
    SeedTransformState(fast);
    AssertTrue(
        fast.TryFastForwardWordHighBitMaskTransformLoop(512, fastBus.TryReadWord, fastBus.TryWriteWord, fastBus.TryReadByte, fastBus.TryWriteByte, out int cycles),
        "word high-bit mask transform should fast-forward");
    AssertEqual(248, cycles);
    fast.Step();
    AssertTrue(fast.Halted, "fast transform routine should stop on the following SLEEP");

    for (int i = 0; i < 16; i++)
    {
        AssertEqual(interpreted.R[i], fast.R[i]);
    }

    AssertEqual(interpreted.PC, fast.PC);
    AssertEqual(interpreted.SR, fast.SR);
    for (uint offset = 0; offset < 16; offset += 2)
    {
        AssertEqual(interpretedBus.ReadWord(WordTable + offset), fastBus.ReadWord(WordTable + offset));
    }

    AssertEqual(interpretedBus.ReadByte(ByteTable), fastBus.ReadByte(ByteTable));

    SyntheticSh2Bus partialBus = new();
    WriteTransformRoutine(partialBus, LoopPc);
    SeedTransformMemory(partialBus);
    Sh2Cpu partial = new(partialBus, "partial");
    SeedTransformState(partial);
    AssertTrue(
        partial.TryFastForwardWordHighBitMaskTransformLoop(62, partialBus.TryReadWord, partialBus.TryWriteWord, partialBus.TryReadByte, partialBus.TryWriteByte, out int partialCycles),
        "word high-bit mask transform should respect the cycle budget");
    AssertEqual(62, partialCycles);
    AssertEqual(LoopPc, partial.PC);
    AssertEqual(2u, partial.R[3]);
    AssertEqual(2u, partial.R[5]);
    AssertEqual(0x20u, partial.R[4]);
    AssertEqual((ushort)0x0001, partialBus.ReadWord(WordTable));
    AssertEqual((ushort)0x0002, partialBus.ReadWord(WordTable + 2));
    AssertEqual((byte)0x82, partialBus.ReadByte(ByteTable));
}

void ThirtyTwoXSh2WordHighBitMaskTransformOuterFastForward()
{
    const uint LoopPc = 0x0600_3002;
    const uint WordTable = 0x0603_1000;
    const uint ByteTable = 0x0603_2000;

    static void WriteOuterRoutine(SyntheticSh2Bus bus, uint loopPc)
    {
        ushort[] opcodes =
        [
            0x64A3, 0x6173, 0x4711, 0x8900, 0x7107, 0x6013, 0x4021, 0x4021,
            0x4021, 0xE100, 0x0914, 0xE500, 0x6373, 0x6033, 0x303C, 0x068D,
            0xD11C, 0x2169, 0x2118, 0x890A, 0x6173, 0x4711, 0x8900, 0x7107,
            0x4121, 0x4121, 0x4121, 0x319C, 0x6210, 0x224B, 0x2120, 0x614C,
            0x6413, 0x4401, 0x911F, 0x2619, 0x6033, 0x303C, 0x0865, 0x7301,
            0x7501, 0xE107, 0x3517, 0x8BE0, 0x7708, 0xD10E, 0x3717, 0x8BCF,
            0x001B
        ];

        for (int i = 0; i < opcodes.Length; i++)
        {
            bus.WriteInstructionWord(loopPc + (uint)(i * 2), opcodes[i]);
        }

        bus.WriteWord(loopPc + 0x86, 0x7FFF);
        bus.WriteLong(loopPc + 0x92, 0x0000_8000);
        bus.WriteLong(loopPc + 0x96, 0x0000_000F);
    }

    static void SeedOuterState(Sh2Cpu cpu)
    {
        cpu.Reset(LoopPc);
        cpu.R[7] = 0;
        cpu.R[8] = WordTable;
        cpu.R[9] = ByteTable;
        cpu.R[10] = 0x80;
    }

    static void SeedOuterMemory(SyntheticSh2Bus bus)
    {
        ushort[] words =
        [
            0x8001, 0x0002, 0x8003, 0x0004, 0x8005, 0x8006, 0x0007, 0x8008,
            0x8009, 0x000A, 0x800B, 0x000C, 0x800D, 0x800E, 0x000F, 0x8010
        ];

        for (uint i = 0; i < words.Length; i++)
        {
            bus.WriteWord(WordTable + (i * 2), words[i]);
        }

        bus.WriteByte(ByteTable, 0xFF);
        bus.WriteByte(ByteTable + 1, 0xFF);
    }

    SyntheticSh2Bus interpretedBus = new();
    WriteOuterRoutine(interpretedBus, LoopPc);
    SeedOuterMemory(interpretedBus);
    Sh2Cpu interpreted = new(interpretedBus, "interpreted");
    SeedOuterState(interpreted);
    interpreted.Run(1024);
    AssertTrue(interpreted.Halted, "interpreter outer transform routine should reach SLEEP");

    SyntheticSh2Bus fastBus = new();
    WriteOuterRoutine(fastBus, LoopPc);
    SeedOuterMemory(fastBus);
    Sh2Cpu fast = new(fastBus, "fast");
    SeedOuterState(fast);
    AssertTrue(
        fast.TryFastForwardWordHighBitMaskTransformOuterLoop(1024, fastBus.TryReadWord, fastBus.TryWriteWord, fastBus.TryReadByte, fastBus.TryWriteByte, out int cycles),
        "word high-bit mask transform outer loop should fast-forward");
    AssertEqual(664, cycles);
    fast.Step();
    AssertTrue(fast.Halted, "fast outer transform routine should stop on the following SLEEP");

    for (int i = 0; i < 16; i++)
    {
        AssertEqual(interpreted.R[i], fast.R[i]);
    }

    AssertEqual(interpreted.PC, fast.PC);
    AssertEqual(interpreted.SR, fast.SR);
    for (uint offset = 0; offset < 32; offset += 2)
    {
        AssertEqual(interpretedBus.ReadWord(WordTable + offset), fastBus.ReadWord(WordTable + offset));
    }

    AssertEqual(interpretedBus.ReadByte(ByteTable), fastBus.ReadByte(ByteTable));
    AssertEqual(interpretedBus.ReadByte(ByteTable + 1), fastBus.ReadByte(ByteTable + 1));

    SyntheticSh2Bus partialBus = new();
    WriteOuterRoutine(partialBus, LoopPc);
    SeedOuterMemory(partialBus);
    Sh2Cpu partial = new(partialBus, "partial");
    SeedOuterState(partial);
    AssertTrue(
        partial.TryFastForwardWordHighBitMaskTransformOuterLoop(332, partialBus.TryReadWord, partialBus.TryWriteWord, partialBus.TryReadByte, partialBus.TryWriteByte, out int partialCycles),
        "word high-bit mask transform outer loop should respect the cycle budget");
    AssertEqual(332, partialCycles);
    AssertEqual(LoopPc, partial.PC);
    AssertEqual(8u, partial.R[7]);
    AssertEqual(8u, partial.R[3]);
    AssertEqual(8u, partial.R[5]);
    AssertEqual(0u, partial.R[4]);
    AssertEqual((byte)0xAD, partialBus.ReadByte(ByteTable));
    AssertEqual((byte)0xFF, partialBus.ReadByte(ByteTable + 1));
}

void ThirtyTwoXSh2ByteLookupWordRowExpandFastForward()
{
    const uint LoopPc = 0x0600_4000;
    const uint Source = 0x0603_0000;
    const uint Lookup = 0x0603_1000;
    const uint Destination = 0x0600_0000;

    static void WriteRowExpandRoutine(SyntheticSh2Bus bus, uint loopPc)
    {
        ushort[] group = [0x6084, 0x600C, 0x4000, 0x00CD, 0x20DB, 0x2E01, 0x3E7C];
        uint pc = loopPc;
        for (int i = 0; i < 8; i++)
        {
            foreach (ushort opcode in group)
            {
                bus.WriteInstructionWord(pc, opcode);
                pc += 2;
            }
        }

        ushort[] tail = [0x3E6C, 0x4910, 0x8D03, 0x0009, 0xD005, 0x402B, 0x0009];
        foreach (ushort opcode in tail)
        {
            bus.WriteInstructionWord(pc, opcode);
            pc += 2;
        }

        bus.WriteInstructionWord(loopPc + 0x7E, 0x001B);
        bus.WriteLong(loopPc + 0x90, loopPc);
    }

    static void SeedRowExpandState(Sh2Cpu cpu)
    {
        cpu.Reset(LoopPc);
        cpu.R[6] = 0x1F0;
        cpu.R[7] = 2;
        cpu.R[8] = Source;
        cpu.R[9] = 2;
        cpu.R[12] = Lookup;
        cpu.R[13] = 0x4000;
        cpu.R[14] = Destination;
    }

    static void SeedRowExpandMemory(SyntheticSh2Bus bus)
    {
        for (int i = 0; i < 16; i++)
        {
            bus.WriteByte(Source + (uint)i, (byte)i);
            bus.WriteWord(Lookup + (uint)(i * 2), (ushort)(0x1000 + i));
        }
    }

    SyntheticSh2Bus interpretedBus = new();
    WriteRowExpandRoutine(interpretedBus, LoopPc);
    SeedRowExpandMemory(interpretedBus);
    Sh2Cpu interpreted = new(interpretedBus, "interpreted");
    SeedRowExpandState(interpreted);
    interpreted.Run(512);
    AssertTrue(interpreted.Halted, "interpreter row expand routine should reach SLEEP");

    SyntheticSh2Bus fastBus = new();
    WriteRowExpandRoutine(fastBus, LoopPc);
    SeedRowExpandMemory(fastBus);
    Sh2Cpu fast = new(fastBus, "fast");
    SeedRowExpandState(fast);
    AssertTrue(
        fast.TryFastForwardByteLookupWordRowExpandLoop(512, fastBus.TryReadByte, fastBus.TryReadWord, fastBus.TryWriteWord, out int cycles),
        "byte lookup word row expand should fast-forward");
    AssertEqual(254, cycles);
    fast.Step();
    AssertTrue(fast.Halted, "fast row expand routine should stop on the following SLEEP");

    for (int i = 0; i < 16; i++)
    {
        AssertEqual(interpreted.R[i], fast.R[i]);
    }

    AssertEqual(interpreted.PC, fast.PC);
    AssertEqual(interpreted.SR, fast.SR);
    for (uint offset = 0; offset < 0x210; offset += 2)
    {
        AssertEqual(interpretedBus.ReadWord(Destination + offset), fastBus.ReadWord(Destination + offset));
    }

    SyntheticSh2Bus partialBus = new();
    WriteRowExpandRoutine(partialBus, LoopPc);
    SeedRowExpandMemory(partialBus);
    Sh2Cpu partial = new(partialBus, "partial");
    SeedRowExpandState(partial);
    AssertTrue(
        partial.TryFastForwardByteLookupWordRowExpandLoop(127, partialBus.TryReadByte, partialBus.TryReadWord, partialBus.TryWriteWord, out int partialCycles),
        "byte lookup word row expand should respect the cycle budget");
    AssertEqual(127, partialCycles);
    AssertEqual(LoopPc, partial.PC);
    AssertEqual(Source + 8, partial.R[8]);
    AssertEqual(1u, partial.R[9]);
    AssertEqual(Destination + 0x200, partial.R[14]);
    AssertEqual(LoopPc, partial.R[0]);
    AssertEqual(0u, partial.SR & 1);
    AssertEqual((ushort)0x5000, partialBus.ReadWord(Destination));
    AssertEqual((ushort)0x5007, partialBus.ReadWord(Destination + 14));
}

void ThirtyTwoXSh2ByteLookupWordStoreStepFastForward()
{
    const uint StepPc = 0x0600_5000;
    const uint Source = 0x0603_0000;
    const uint Lookup = 0x0603_1000;
    const uint Destination = 0x0600_0000;

    ushort[] opcodes = [0x6084, 0x600C, 0x4000, 0x00CD, 0x20DB, 0x2E01, 0x3E7C, 0x001B];

    static void SeedStepState(Sh2Cpu cpu)
    {
        cpu.Reset(StepPc);
        cpu.R[7] = 2;
        cpu.R[8] = Source;
        cpu.R[12] = Lookup;
        cpu.R[13] = 0x4000;
        cpu.R[14] = Destination;
    }

    static void SeedStepMemory(SyntheticSh2Bus bus)
    {
        bus.WriteByte(Source, 0x03);
        bus.WriteWord(Lookup + 6, 0x1234);
    }

    SyntheticSh2Bus interpretedBus = new();
    for (int i = 0; i < opcodes.Length; i++)
    {
        interpretedBus.WriteInstructionWord(StepPc + (uint)(i * 2), opcodes[i]);
    }

    SeedStepMemory(interpretedBus);
    Sh2Cpu interpreted = new(interpretedBus, "interpreted");
    SeedStepState(interpreted);
    interpreted.Run(32);
    AssertTrue(interpreted.Halted, "interpreter row expand step should reach SLEEP");

    SyntheticSh2Bus fastBus = new();
    for (int i = 0; i < opcodes.Length; i++)
    {
        fastBus.WriteInstructionWord(StepPc + (uint)(i * 2), opcodes[i]);
    }

    SeedStepMemory(fastBus);
    Sh2Cpu fast = new(fastBus, "fast");
    SeedStepState(fast);
    AssertTrue(
        fast.TryFastForwardByteLookupWordStoreStep(25, fastBus.TryReadByte, fastBus.TryReadWord, fastBus.TryWriteWord, out int cycles),
        "byte lookup word store step should fast-forward");
    AssertEqual(25, cycles);
    fast.Step();
    AssertTrue(fast.Halted, "fast row expand step should stop on the following SLEEP");

    for (int i = 0; i < 16; i++)
    {
        AssertEqual(interpreted.R[i], fast.R[i]);
    }

    AssertEqual(interpreted.PC, fast.PC);
    AssertEqual(interpreted.SR, fast.SR);
    AssertEqual(interpretedBus.ReadWord(Destination), fastBus.ReadWord(Destination));
}

void ThirtyTwoXSh2MovLiteralTstBfPollLoopFastForward()
{
    const uint LoopPc = 0x0600_0100;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xD102); // MOV.L @(2,PC),R1
    bus.WriteInstructionWord(LoopPc + 2, 0x6213); // MOV R1,R2
    bus.WriteInstructionWord(LoopPc + 4, 0x2228); // TST R2,R2
    bus.WriteInstructionWord(LoopPc + 6, 0x8BFB); // BF loop
    bus.WriteLong(LoopPc + 0x0C, 0x2000_4020);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    AssertTrue(cpu.TryFastForwardMovLiteralTstBfPollLoop(300, out int cycles), "MOV literal/TST/BF register poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x2000_4020u, cpu.R[1]);
    AssertEqual(0x2000_4020u, cpu.R[2]);
}

void ThirtyTwoXSh2MovLiteralLongTstBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_22A4;
    const uint FlagAddress = 0x0600_3150;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xD00D); // MOV.L @(13,PC),R0
    bus.WriteInstructionWord(LoopPc + 2, 0x6002); // MOV.L @R0,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x2008); // TST R0,R0
    bus.WriteInstructionWord(LoopPc + 6, 0x89FB); // BT loop
    bus.WriteLong(LoopPc + 0x38, FlagAddress);
    bus.WriteLong(FlagAddress, 0x0000_0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    AssertTrue(cpu.TryFastForwardMovLiteralLongTstBtPollLoop(300, out int cycles), "MOV literal/MOV.L/TST/BT zero poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc + 6);
    AssertTrue(cpu.TryFastForwardMovLiteralLongTstBtPollLoop(300, out _), "MOV literal/MOV.L/TST/BT zero poll should fast-forward from the branch instruction");
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteLong(FlagAddress, 0x0000_0001);
    AssertTrue(!cpu.TryFastForwardMovLiteralLongTstBtPollLoop(300, out _), "nonzero MOV literal long poll should fall back to normal execution");
}

void ThirtyTwoXSh2MovLiteralWordTstBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_08F8;
    const uint PollAddress = 0x0600_0000;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xD10D); // MOV.L @(13,PC),R1
    bus.WriteInstructionWord(LoopPc + 2, 0x6211); // MOV.W @R1,R2
    bus.WriteInstructionWord(LoopPc + 4, 0x2228); // TST R2,R2
    bus.WriteInstructionWord(LoopPc + 6, 0x89FB); // BT loop
    bus.WriteLong(LoopPc + 0x38, PollAddress);
    bus.WriteWord(PollAddress, 0x0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    AssertTrue(cpu.TryFastForwardMovLiteralWordTstBtPollLoop(300, out int cycles), "MOV literal/MOV.W/TST/BT zero poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(PollAddress, cpu.R[1]);
    AssertEqual(0u, cpu.R[2]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc + 6);
    AssertTrue(cpu.TryFastForwardMovLiteralWordTstBtPollLoop(300, out _), "MOV literal/MOV.W/TST/BT zero poll should fast-forward from the branch instruction");
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteWord(PollAddress, 0x0001);
    AssertTrue(!cpu.TryFastForwardMovLiteralWordTstBtPollLoop(300, out _), "nonzero MOV literal word poll should fall back to normal execution");
}

void ThirtyTwoXSh2MovLiteralWordCmpEqBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_0100;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xD005); // MOV.L @(5,PC),R0
    bus.WriteInstructionWord(LoopPc + 2, 0x6001); // MOV.W @R0,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x8800); // CMP/EQ #0,R0
    bus.WriteInstructionWord(LoopPc + 6, 0x89FB); // BT loop
    bus.WriteLong(LoopPc + 0x18, 0x2000_4020);
    bus.WriteWord(0x2000_4020, 0x0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    AssertTrue(cpu.TryFastForwardMovLiteralWordCmpEqBtPollLoop(300, out int cycles), "MOV literal/MOV.W/CMP/EQ/BT zero poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    bus.WriteWord(0x2000_4020, 0x0001);
    AssertTrue(!cpu.TryFastForwardMovLiteralWordCmpEqBtPollLoop(300, out _), "non-matching poll value should fall back to normal execution");
}

void ThirtyTwoXSh2MovLiteralByteCmpEqBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_0460;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xD809); // MOV.L @(9,PC),R8
    bus.WriteInstructionWord(LoopPc + 2, 0x6080); // MOV.B @R8,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x8800); // CMP/EQ #0,R0
    bus.WriteInstructionWord(LoopPc + 6, 0x89FB); // BT loop
    bus.WriteLong(LoopPc + 0x28, 0x2000_4020);
    bus.WriteByte(0x2000_4020, 0x00);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    AssertTrue(cpu.TryFastForwardMovLiteralByteCmpEqBtPollLoop(300, out int cycles), "MOV literal/MOV.B/CMP/EQ/BT zero poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x2000_4020u, cpu.R[8]);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc + 4);
    AssertTrue(cpu.TryFastForwardMovLiteralByteCmpEqBtPollLoop(300, out _), "MOV literal/MOV.B/CMP/EQ/BT zero poll should fast-forward from the compare instruction");
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteByte(0x2000_4020, 0x01);
    AssertTrue(!cpu.TryFastForwardMovLiteralByteCmpEqBtPollLoop(300, out _), "non-matching byte poll value should fall back to normal execution");
}

void ThirtyTwoXSh2WordCmpEqBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_0200;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x6011); // MOV.W @R1,R0
    bus.WriteInstructionWord(LoopPc + 2, 0x8800); // CMP/EQ #0,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x89FC); // BT loop
    bus.WriteWord(0x2000_4020, 0x0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[1] = 0x2000_4020;
    AssertTrue(cpu.TryFastForwardWordCmpEqBtPollLoop(300, out int cycles), "MOV.W/CMP/EQ/BT zero poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(0x2000_4020u, cpu.R[1]);
    AssertEqual(1u, cpu.SR & 1);

    bus.WriteWord(0x2000_4020, 0x0001);
    AssertTrue(!cpu.TryFastForwardWordCmpEqBtPollLoop(300, out _), "non-matching compact poll value should fall back to normal execution");
}

void ThirtyTwoXSh2StableWordPairCmpEqBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_2816;
    const uint FlagAddress = 0x2603_2B90;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x6211); // MOV.W @R1,R2
    bus.WriteInstructionWord(LoopPc + 2, 0x3200); // CMP/EQ R0,R2
    bus.WriteInstructionWord(LoopPc + 4, 0x89FC); // BT loop
    bus.WriteWord(FlagAddress, 0x0010);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc + 4);
    cpu.R[0] = 0x0000_0010;
    cpu.R[1] = FlagAddress;
    cpu.R[2] = 0x0000_0010;
    AssertTrue(cpu.TryFastForwardStableWordPairCmpEqBtPollLoop(512, out int cycles), "stable word pair CMP/EQ/BT poll should fast-forward from branch");
    AssertEqual(512, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x0000_0010u, cpu.R[0]);
    AssertEqual(0x0000_0010u, cpu.R[2]);
    AssertEqual(1u, cpu.SR & 1);

    bus.WriteWord(FlagAddress, 0x0011);
    AssertTrue(!cpu.TryFastForwardStableWordPairCmpEqBtPollLoop(512, out _), "changed word pair value should fall back to normal execution");
}

void ThirtyTwoXSh2LongRegisterCmpEqBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_0D8E;
    const uint FlagAddress = 0x0600_8234;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x6202); // MOV.L @R0,R2
    bus.WriteInstructionWord(LoopPc + 2, 0x3120); // CMP/EQ R2,R1
    bus.WriteInstructionWord(LoopPc + 4, 0x89FC); // BT loop
    bus.WriteLong(FlagAddress, 0x1234_5678);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc + 4);
    cpu.R[0] = FlagAddress;
    cpu.R[1] = 0x1234_5678;
    cpu.R[2] = 0x1234_5678;
    AssertTrue(cpu.TryFastForwardLongRegisterCmpEqBtPollLoop(512, out int cycles), "MOV.L/CMP/EQ/BT long poll should fast-forward from branch");
    AssertEqual(512, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x1234_5678u, cpu.R[1]);
    AssertEqual(0x1234_5678u, cpu.R[2]);
    AssertEqual(1u, cpu.SR & 1);

    Sh2Cpu comparePcCpu = new(bus, "test");
    comparePcCpu.Reset(LoopPc + 2);
    comparePcCpu.R[0] = FlagAddress;
    comparePcCpu.R[1] = 0x1234_5678;
    AssertTrue(comparePcCpu.TryFastForwardLongRegisterCmpEqBtPollLoop(512, out _), "MOV.L/CMP/EQ/BT long poll should fast-forward from compare");

    bus.WriteLong(FlagAddress, 0x1234_5679);
    AssertTrue(!cpu.TryFastForwardLongRegisterCmpEqBtPollLoop(512, out _), "changed long value should fall back to normal execution");
}

void ThirtyTwoXSh2WordCmpEqBfPollLoopFastForward()
{
    const uint LoopPc = 0x0600_30D0;
    const uint FlagAddress = 0x2000_402E;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x6011); // MOV.W @R1,R0
    bus.WriteInstructionWord(LoopPc + 2, 0x8800); // CMP/EQ #0,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x8BFC); // BF loop while nonzero
    bus.WriteWord(FlagAddress, 1);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc + 4);
    cpu.R[1] = FlagAddress;
    AssertTrue(cpu.TryFastForwardWordCmpEqBfPollLoop(300, out int cycles), "MOV.W/CMP/EQ/BF nonzero poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(1u, cpu.R[0]);
    AssertEqual(0u, cpu.SR & 1);

    bus.WriteWord(FlagAddress, 0);
    AssertTrue(!cpu.TryFastForwardWordCmpEqBfPollLoop(300, out _), "matching compact poll value should fall back to normal execution");
}

void ThirtyTwoXSh2WordTstBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_1620;
    const uint FlagAddress = 0x2600_0802;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x6121); // MOV.W @R2,R1
    bus.WriteInstructionWord(LoopPc + 2, 0x2118); // TST R1,R1
    bus.WriteInstructionWord(LoopPc + 4, 0x89FC); // BT loop while zero
    bus.WriteWord(FlagAddress, 0);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[2] = FlagAddress;
    AssertTrue(cpu.TryFastForwardWordTstBtPollLoop(300, out int cycles), "MOV.W/TST/BT zero poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[1]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc + 4);
    cpu.R[2] = FlagAddress;
    AssertTrue(cpu.TryFastForwardWordTstBtPollLoop(300, out _), "MOV.W/TST/BT zero poll should fast-forward from the branch instruction");
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteWord(FlagAddress, 1);
    AssertTrue(!cpu.TryFastForwardWordTstBtPollLoop(300, out _), "nonzero TST poll value should fall back to normal execution");

    bus.WriteInstructionWord(LoopPc + 2, 0x2168); // TST R6,R1
    bus.WriteWord(FlagAddress, 0x7FFF);
    cpu.Reset(LoopPc);
    cpu.R[2] = FlagAddress;
    cpu.R[6] = 0x8000;
    AssertTrue(cpu.TryFastForwardWordTstBtPollLoop(300, out _), "MOV.W/TST mask/BT zero poll should fast-forward");
    AssertEqual(0x00007FFFu, cpu.R[1]);
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteWord(FlagAddress, 0x8000);
    AssertTrue(!cpu.TryFastForwardWordTstBtPollLoop(300, out _), "set masked TST poll value should fall back to normal execution");
}

void ThirtyTwoXSh2WordTstBfPollLoopFastForward()
{
    const uint LoopPc = 0x0600_0200;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x6011); // MOV.W @R1,R0
    bus.WriteInstructionWord(LoopPc + 2, 0x2008); // TST R0,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x8BFC); // BF loop
    bus.WriteWord(0x2000_4020, 0x0001);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[1] = 0x2000_4020;
    AssertTrue(cpu.TryFastForwardWordTstBfPollLoop(300, out int cycles), "MOV.W/TST/BF nonzero poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(1u, cpu.R[0]);
    AssertEqual(0x2000_4020u, cpu.R[1]);
    AssertEqual(0u, cpu.SR & 1);

    bus.WriteWord(0x2000_4020, 0x0000);
    AssertTrue(!cpu.TryFastForwardWordTstBfPollLoop(300, out _), "zero poll value should fall back to normal execution");

    bus.WriteInstructionWord(LoopPc + 0, 0x6311); // MOV.W @R1,R3
    bus.WriteInstructionWord(LoopPc + 2, 0x2368); // TST R6,R3
    bus.WriteWord(0x2000_4020, 0x8000);
    cpu.Reset(LoopPc);
    cpu.R[1] = 0x2000_4020;
    cpu.R[6] = 0x8000;
    AssertTrue(cpu.TryFastForwardWordTstBfPollLoop(300, out _), "MOV.W/TST mask/BF nonzero poll should fast-forward");
    AssertEqual(0xFFFF8000u, cpu.R[3]);
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteWord(0x2000_4020, 0x7FFF);
    AssertTrue(!cpu.TryFastForwardWordTstBfPollLoop(300, out _), "clear masked TST poll value should fall back to normal execution");
}

void ThirtyTwoXSh2WordDisplacementTstBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_0894;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x8580); // MOV.W @(0,R8),R0
    bus.WriteInstructionWord(LoopPc + 2, 0x2008); // TST R0,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x89FC); // BT loop
    bus.WriteWord(0x2000_4020, 0x0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[8] = 0x2000_4020;
    AssertTrue(cpu.TryFastForwardWordDisplacementTstBtPollLoop(300, out int cycles), "MOV.W displacement/TST/BT zero poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc + 4);
    cpu.R[8] = 0x2000_4020;
    AssertTrue(cpu.TryFastForwardWordDisplacementTstBtPollLoop(300, out _), "MOV.W displacement/TST/BT zero poll should fast-forward from the branch instruction");
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteWord(0x2000_4020, 0x0001);
    AssertTrue(!cpu.TryFastForwardWordDisplacementTstBtPollLoop(300, out _), "nonzero displacement poll value should fall back to normal execution");
}

void ThirtyTwoXSh2LongTstBtPaddedPollLoopFastForward()
{
    const uint LoopPc = 0x0600_4C0C;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x6012); // MOV.L @R1,R0
    bus.WriteInstructionWord(LoopPc + 2, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 4, 0x2008); // TST R0,R0
    bus.WriteInstructionWord(LoopPc + 6, 0x89FB); // BT loop
    bus.WriteLong(0x2000_4020, 0x0000_0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[1] = 0x2000_4020;
    AssertTrue(cpu.TryFastForwardLongTstBtPaddedPollLoop(300, out int cycles), "padded MOV.L/TST/BT zero poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc + 6);
    cpu.R[1] = 0x2000_4020;
    AssertTrue(cpu.TryFastForwardLongTstBtPaddedPollLoop(300, out _), "padded MOV.L/TST/BT zero poll should fast-forward from the branch instruction");
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteLong(0x2000_4020, 0x0000_0001);
    AssertTrue(!cpu.TryFastForwardLongTstBtPaddedPollLoop(300, out _), "nonzero padded long poll should fall back to normal execution");
}

void ThirtyTwoXSh2LongMaskedChangeBtSDelayPollLoopFastForward()
{
    const uint LoopPc = 0x0204_DF30;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x3200); // CMP/EQ R0,R2
    bus.WriteInstructionWord(LoopPc + 2, 0x6012); // MOV.L @R1,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x8DFC); // BT/S loop
    bus.WriteInstructionWord(LoopPc + 6, 0x2039); // AND R3,R0
    bus.WriteLong(0x0600_1170, 0x0000_0002);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[0] = 0x0000_0002;
    cpu.R[1] = 0x0600_1170;
    cpu.R[2] = 0x0000_0002;
    cpu.R[3] = 0x0000_0003;
    AssertTrue(cpu.TryFastForwardLongMaskedChangeBtSDelayPollLoop(300, out int cycles), "masked long change poll should fast-forward while the masked value is unchanged");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x0000_0002u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc + 4);
    cpu.R[0] = 0x0000_0002;
    cpu.R[1] = 0x0600_1170;
    cpu.R[2] = 0x0000_0002;
    cpu.R[3] = 0x0000_0003;
    AssertTrue(cpu.TryFastForwardLongMaskedChangeBtSDelayPollLoop(300, out _), "masked long change poll should fast-forward from the branch instruction");
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x0000_0002u, cpu.R[0]);

    bus.WriteLong(0x0600_1170, 0x0000_0001);
    AssertTrue(!cpu.TryFastForwardLongMaskedChangeBtSDelayPollLoop(300, out _), "changed masked long value should fall back to the interpreter");
}

void ThirtyTwoXSh2GbrLongMaskedOrCompareBfPollLoopFastForward()
{
    const uint LoopPc = 0x0600_03FC;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xC608); // MOV.L @(32,GBR),R0
    bus.WriteInstructionWord(LoopPc + 2, 0x6303); // MOV R0,R3
    bus.WriteInstructionWord(LoopPc + 4, 0x2019); // AND R1,R0
    bus.WriteInstructionWord(LoopPc + 6, 0xCB20); // OR #32,R0
    bus.WriteInstructionWord(LoopPc + 8, 0x3020); // CMP/EQ R2,R0
    bus.WriteInstructionWord(LoopPc + 10, 0x8BF9); // BF loop
    bus.WriteLong(0x2000_4020, 0x0000_0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc + 10);
    cpu.SetGbr(0x2000_4000);
    cpu.R[1] = 0xFFFF_FF00;
    cpu.R[2] = 0x4D41_4320;
    AssertTrue(cpu.TryFastForwardGbrLongMaskedOrCompareBfPollLoop(1024, out int cycles), "GBR long masked OR compare BF poll should fast-forward while the signature is absent");
    AssertEqual(1024, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x0000_0020u, cpu.R[0]);
    AssertEqual(0x0000_0000u, cpu.R[3]);
    AssertEqual(0u, cpu.SR & 1);

    bus.WriteLong(0x2000_4020, 0x4D41_4300);
    AssertTrue(!cpu.TryFastForwardGbrLongMaskedOrCompareBfPollLoop(1024, out _), "matching masked OR signature should fall back so the loop can exit");
}

void ThirtyTwoXSh2WordIncrementGbrZeroBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_05EC;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x6011); // MOV.W @R1,R0
    bus.WriteInstructionWord(LoopPc + 2, 0x7001); // ADD #1,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x2101); // MOV.W R0,@R1
    bus.WriteInstructionWord(LoopPc + 6, 0xC516); // MOV.W @(44,GBR),R0
    bus.WriteInstructionWord(LoopPc + 8, 0x8800); // CMP/EQ #0,R0
    bus.WriteInstructionWord(LoopPc + 10, 0x89F9); // BT loop
    bus.WriteWord(0x0600_49E2, 0x0007);
    bus.WriteWord(0x2000_402C, 0x0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[1] = 0x0600_49E2;
    cpu.SetGbr(0x2000_4000);
    AssertTrue(cpu.TryFastForwardWordIncrementGbrZeroBtPollLoop(120, bus.TryWriteWord, out int cycles), "word increment/GBR zero poll should fast-forward while the guard word is zero");
    AssertEqual(120, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual((ushort)0x0011, bus.ReadWord(0x0600_49E2));
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc + 10);
    cpu.R[1] = 0x0600_49E2;
    cpu.SetGbr(0x2000_4000);
    AssertTrue(cpu.TryFastForwardWordIncrementGbrZeroBtPollLoop(120, bus.TryWriteWord, out _), "word increment/GBR zero poll should fast-forward from the branch instruction");
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteWord(0x2000_402C, 0x0001);
    AssertTrue(!cpu.TryFastForwardWordIncrementGbrZeroBtPollLoop(120, bus.TryWriteWord, out _), "nonzero guard word should fall back to the interpreter");
}

void ThirtyTwoXSh2ByteTstBfPollLoopFastForward()
{
    const uint LoopPc = 0x0600_0F06;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x6010); // MOV.B @R1,R0
    bus.WriteInstructionWord(LoopPc + 2, 0x2008); // TST R0,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x8BFC); // BF loop
    bus.WriteByte(0x2000_4020, 0x7F);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[1] = 0x2000_4020;
    AssertTrue(cpu.TryFastForwardByteTstBfPollLoop(300, out int cycles), "MOV.B/TST/BF nonzero poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x7Fu, cpu.R[0]);
    AssertEqual(0x2000_4020u, cpu.R[1]);
    AssertEqual(0u, cpu.SR & 1);

    cpu.Reset(LoopPc + 2);
    cpu.R[1] = 0x2000_4020;
    AssertTrue(cpu.TryFastForwardByteTstBfPollLoop(300, out _), "MOV.B/TST/BF poll should fast-forward from the TST instruction");
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteByte(0x2000_4020, 0x00);
    AssertTrue(!cpu.TryFastForwardByteTstBfPollLoop(300, out _), "zero byte poll value should fall back to normal execution");
}

void ThirtyTwoXSh2ByteDisplacementTstImmediateBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_1672;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x8444); // MOV.B @(4,R4),R0
    bus.WriteInstructionWord(LoopPc + 2, 0xC840); // TST #$40,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x89FC); // BT loop
    bus.WriteByte(0x2000_4024, 0x00);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[4] = 0x2000_4020;
    AssertTrue(cpu.TryFastForwardByteDisplacementTstImmediateBtPollLoop(300, out int cycles), "MOV.B displacement/TST immediate/BT zero-bit poll should fast-forward");
    AssertEqual(300, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc + 4);
    cpu.R[4] = 0x2000_4020;
    AssertTrue(cpu.TryFastForwardByteDisplacementTstImmediateBtPollLoop(300, out _), "MOV.B displacement/TST immediate/BT zero-bit poll should fast-forward from the branch instruction");
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteByte(0x2000_4024, 0x40);
    AssertTrue(!cpu.TryFastForwardByteDisplacementTstImmediateBtPollLoop(300, out _), "set bit should fall back to normal execution");
}

void ThirtyTwoXSh2PeripheralByteTstImmediatePollFastForward()
{
    const uint MasterPc = 0x0600_0000;
    const uint SlavePc = 0x0600_1000;
    ThirtyTwoXDevice device = new();
    WriteSh2WordForTest(device, MasterPc, 0x001B, cpuIndex: 0); // SLEEP
    WriteSh2WordForTest(device, SlavePc + 0, 0x8444, cpuIndex: 1); // MOV.B @(4,R4),R0
    WriteSh2WordForTest(device, SlavePc + 2, 0xC840, cpuIndex: 1); // TST #$40,R0
    WriteSh2WordForTest(device, SlavePc + 4, 0x89FC, cpuIndex: 1); // BT loop
    device.ResetSh2(MasterPc, SlavePc);
    device.RestoreState(device.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
    });
    device.SlaveSh2.R[4] = 0xFFFF_FE00;
    WriteSh2ByteForTest(device, 0xFFFF_FE04, 0x00, cpuIndex: 1);

    int steps = device.RunSh2Cycles(512);
    AssertTrue(steps > 0, "32X peripheral poll fixture should run SH-2 cycles");
    AssertTrue(device.Sh2FastPathHits > 0, "peripheral byte TST immediate poll should fast-forward through the device scheduler");
    AssertEqual(SlavePc, device.SlaveSh2.PC);
    AssertEqual(1u, device.SlaveSh2.SR & 1);
}

void ThirtyTwoXSh2ByteDisplacementZeroWaitDtBfLoopFastForward()
{
    const uint LoopPc = 0x0600_38EE;
    const uint ByteAddress = 0x0600_3AF1;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0x8411); // MOV.B @(1,R1),R0
    bus.WriteInstructionWord(LoopPc + 2, 0x2008); // TST R0,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x8B21); // BF exit
    bus.WriteInstructionWord(LoopPc + 6, 0x4210); // DT R2
    bus.WriteInstructionWord(LoopPc + 8, 0x8BFA); // BF loop
    bus.WriteByte(ByteAddress, 0x00);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc + 2);
    cpu.R[1] = ByteAddress - 1;
    cpu.R[2] = 1000;
    AssertTrue(cpu.TryFastForwardByteDisplacementZeroWaitDtBfLoop(4096, out int cycles), "byte displacement zero wait loop should fast-forward while the byte is zero");
    AssertEqual(4092, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(318u, cpu.R[2]);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(0u, cpu.SR & 1);

    cpu.Reset(LoopPc + 8);
    cpu.R[1] = ByteAddress - 1;
    cpu.R[2] = 1000;
    AssertTrue(cpu.TryFastForwardByteDisplacementZeroWaitDtBfLoop(4096, out _), "byte displacement zero wait loop should fast-forward from the loop branch");
    AssertEqual(LoopPc, cpu.PC);
    AssertTrue(cpu.R[2] < 1000, "branch-entry byte wait loop should consume at least one countdown iteration");

    cpu.R[2] = 2;
    AssertTrue(cpu.TryFastForwardByteDisplacementZeroWaitDtBfLoop(4096, out _), "byte displacement zero wait loop should finish when DT reaches zero");
    AssertEqual(LoopPc + 10, cpu.PC);
    AssertEqual(0u, cpu.R[2]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc);
    cpu.R[1] = ByteAddress - 1;
    cpu.R[2] = 1000;
    bus.WriteByte(ByteAddress, 0x80);
    AssertTrue(!cpu.TryFastForwardByteDisplacementZeroWaitDtBfLoop(4096, out _), "nonzero wait byte should fall back to the interpreter");

    const uint OuterPc = 0x0600_159C;
    const uint InnerPc = 0x0600_15B8;
    const uint OuterFlagAddress = 0x0600_1798;
    const uint InnerByteAddress = 0x0600_1789;
    SyntheticSh2Bus nestedBus = new();
    nestedBus.WriteInstructionWord(OuterPc + 0, 0x90FC); // MOV.W @($1F8,PC),R0
    nestedBus.WriteInstructionWord(OuterPc + 2, 0x2008); // TST R0,R0
    nestedBus.WriteInstructionWord(OuterPc + 4, 0x890A); // BT inner wait
    nestedBus.WriteWord(OuterFlagAddress, 0x0000);
    nestedBus.WriteInstructionWord(InnerPc + 0, 0x8411); // MOV.B @(1,R1),R0
    nestedBus.WriteInstructionWord(InnerPc + 2, 0x2008); // TST R0,R0
    nestedBus.WriteInstructionWord(InnerPc + 4, 0x8B16); // BF exit
    nestedBus.WriteInstructionWord(InnerPc + 6, 0x4210); // DT R2
    nestedBus.WriteInstructionWord(InnerPc + 8, 0x8BEC); // BF outer gate
    nestedBus.WriteByte(InnerByteAddress, 0x00);
    Sh2Cpu nested = new(nestedBus, "test");
    nested.Reset(InnerPc + 8);
    nested.R[1] = InnerByteAddress - 1;
    nested.R[2] = 1000;
    AssertTrue(nested.TryFastForwardOuterWordZeroByteDisplacementWaitDtBfLoop(4096, out int nestedCycles), "outer word zero plus byte displacement wait should fast-forward from the inner loop branch");
    AssertTrue(nestedCycles > 0, "nested byte wait loop should report consumed cycles");
    AssertEqual(OuterPc, nested.PC);
    AssertTrue(nested.R[2] < 1000, "nested byte wait loop should consume at least one countdown iteration");

    nestedBus.WriteWord(OuterFlagAddress, 0x0001);
    AssertTrue(!nested.TryFastForwardOuterWordZeroByteDisplacementWaitDtBfLoop(4096, out _), "nonzero outer word should fall back to the interpreter");
}

void ThirtyTwoXSh2TstBfsDelayAddLoopFastForward()
{
    const uint LoopPc = 0x0600_0200;
    SyntheticSh2Bus interpretedBus = new();
    LoadLoop(interpretedBus);
    Sh2Cpu interpreted = new(interpretedBus, "test");
    interpreted.Reset(LoopPc);
    interpreted.R[4] = 7;
    interpreted.InstructionObserver = _ => { };
    interpreted.Run(24);

    SyntheticSh2Bus fastBus = new();
    LoadLoop(fastBus);
    Sh2Cpu fast = new(fastBus, "test");
    fast.Reset(LoopPc);
    fast.R[4] = 7;
    AssertTrue(fast.TryFastForwardTstBfsDelayAddLoop(128, out int cycles), "TST/BF/S delay ADD loop should fast-forward");
    AssertEqual(21, cycles);
    fast.Step();
    fast.Step();
    fast.Step();
    fast.Step();

    AssertEqual(interpreted.PC, fast.PC);
    AssertEqual(interpreted.R[4], fast.R[4]);
    AssertEqual(interpreted.SR, fast.SR);

    static void LoadLoop(SyntheticSh2Bus bus)
    {
        bus.WriteInstructionWord(LoopPc + 0, 0x2448); // TST R4,R4
        bus.WriteInstructionWord(LoopPc + 2, 0x8FFD); // BF/S LoopPc
        bus.WriteInstructionWord(LoopPc + 4, 0x74FF); // ADD #-1,R4
        bus.WriteInstructionWord(LoopPc + 6, 0x001B); // SLEEP
    }
}

void ThirtyTwoXSh2TwoStageWordPollRingFastForward()
{
    const uint SetupEntryPc = 0x0600_07F4;
    const uint SetupPc = 0x0600_03C0;
    const uint PollPc = 0x0600_0820;
    const uint BranchPc = 0x0600_0826;
    const uint FirstAddress = 0xFFFF_FF40;
    const uint PollAddress = 0x0600_079E;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(SetupEntryPc + 0, 0xADE4); // BRA setup
    bus.WriteInstructionWord(SetupEntryPc + 2, 0x0009); // NOP
    bus.WriteInstructionWord(SetupPc + 0, 0xD834); // MOV.L @(literal,PC),R8
    bus.WriteInstructionWord(SetupPc + 2, 0x6181); // MOV.W @R8,R1
    bus.WriteInstructionWord(SetupPc + 4, 0x8581); // MOV.W @(2,R8),R0
    bus.WriteInstructionWord(SetupPc + 6, 0x3100); // CMP/EQ R0,R1
    bus.WriteInstructionWord(SetupPc + 8, 0x8B02); // BF exit
    bus.WriteInstructionWord(SetupPc + 10, 0xA229); // BRA poll
    bus.WriteInstructionWord(SetupPc + 12, 0x0009); // NOP
    bus.WriteInstructionWord(PollPc + 0, 0xD808); // MOV.L @(literal,PC),R8
    bus.WriteInstructionWord(PollPc + 2, 0x6081); // MOV.W @R8,R0
    bus.WriteInstructionWord(PollPc + 4, 0x2008); // TST R0,R0
    bus.WriteInstructionWord(BranchPc, 0x89E5); // BT setup entry
    bus.WriteLong(SetupPc + 0xD4, FirstAddress);
    bus.WriteLong(PollPc + 0x24, PollAddress);
    bus.WriteWord(FirstAddress, 0x0000);
    bus.WriteWord(FirstAddress + 2, 0x0000);
    bus.WriteWord(PollAddress, 0x0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(BranchPc);
    SetSh2Property(cpu, nameof(Sh2Cpu.SR), 0x0000_00F1u);
    AssertTrue(cpu.TryFastForwardTwoStageWordZeroPollRing(2000, out int cycles), "two-stage word zero poll ring should fast-forward");
    AssertEqual(512, cycles);
    AssertEqual(BranchPc, cpu.PC);
    AssertEqual(PollAddress, cpu.R[8]);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(0u, cpu.R[1]);

    bus.WriteWord(PollAddress, 0x0001);
    AssertTrue(!cpu.TryFastForwardTwoStageWordZeroPollRing(2000, out _), "nonzero poll word should not fast-forward");
}

void ThirtyTwoXSh2SdramFlagTaskletFastForward()
{
    const uint Pc = 0x0600_45E4;
    const uint FlagAddress = 0x0600_74BC;
    const uint ValueAddress = 0x0600_74C8;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(Pc + 0, 0xD132); // MOV.L literal,R1
    bus.WriteInstructionWord(Pc + 2, 0x6011); // MOV.W @R1,R0
    bus.WriteInstructionWord(Pc + 4, 0xC801); // TST #1,R0
    bus.WriteInstructionWord(Pc + 6, 0x8950); // BT return
    bus.WriteInstructionWord(Pc + 8, 0xDE2F); // MOV.L literal,R14
    bus.WriteInstructionWord(Pc + 10, 0x61E2); // MOV.L @R14,R1
    bus.WriteInstructionWord(Pc + 12, 0xD22D); // MOV.L literal,R2
    bus.WriteInstructionWord(Pc + 14, 0x3210); // CMP/EQ R1,R2
    bus.WriteInstructionWord(Pc + 16, 0x894B); // BT return
    bus.WriteInstructionWord(0x0600_468E, 0x000B); // RTS
    bus.WriteInstructionWord(0x0600_4690, 0x4F26); // LDS.L @R15+,PR
    bus.WriteLong(0x0600_46B0, FlagAddress);
    bus.WriteLong(0x0600_46AC, ValueAddress);
    bus.WriteLong(0x0600_46A8, 0x0000_0080);
    bus.WriteWord(FlagAddress, 0x0001);
    bus.WriteLong(ValueAddress, 0x0000_0080);
    bus.WriteLong(0x0603_EFFC, 0x1234_5678);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(Pc);
    SetSh2Property(cpu, nameof(Sh2Cpu.PR), 0x0600_01B6u);
    cpu.R[15] = 0x0603_EFFC;
    AssertTrue(cpu.TryFastForwardSdramFlagTaskletReturn(32, out int cycles), "SDRAM flag tasklet should fast-forward when the guarded value matches");
    AssertEqual(10, cycles);
    AssertEqual(0x0600_01B6u, cpu.PC);
    AssertEqual(0x1234_5678u, cpu.PR);
    AssertEqual(0x0603_F000u, cpu.R[15]);
    AssertEqual(FlagAddress, cpu.R[1]);
    AssertEqual(ValueAddress, cpu.R[14]);
    AssertEqual(0x0000_0080u, cpu.R[2]);
    AssertEqual(1u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(Pc);
    SetSh2Property(cpu, nameof(Sh2Cpu.PR), 0x0600_01B6u);
    cpu.R[15] = 0x0603_EFFC;
    bus.WriteLong(ValueAddress, 0x0000_0081);
    AssertTrue(!cpu.TryFastForwardSdramFlagTaskletReturn(32, out _), "nonmatching guarded value should fall back to the interpreter");
}

void ThirtyTwoXSh2SdramFlagTaskletDispatcherLoopFastForward()
{
    const uint LoopPc = 0x0600_01AA;
    const uint TaskletPc = 0x0600_45E4;
    const uint PointerAddress = 0x0600_52A8;
    const uint FlagAddress = 0x0600_74BC;
    const uint ValueAddress = 0x0600_74C8;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xDE03); // MOV.L literal,R14
    bus.WriteInstructionWord(LoopPc + 2, 0x60E2); // MOV.L @R14,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x4F22); // STS.L PR,@-R15
    bus.WriteInstructionWord(LoopPc + 6, 0x400B); // JSR @R0
    bus.WriteInstructionWord(LoopPc + 8, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 10, 0xAFF9); // BRA loop
    bus.WriteInstructionWord(LoopPc + 12, 0x0009); // NOP
    bus.WriteLong(0x0600_01B8, PointerAddress);
    bus.WriteLong(PointerAddress, TaskletPc);
    bus.WriteInstructionWord(TaskletPc + 0, 0xD132); // MOV.L literal,R1
    bus.WriteInstructionWord(TaskletPc + 2, 0x6011); // MOV.W @R1,R0
    bus.WriteInstructionWord(TaskletPc + 4, 0xC801); // TST #1,R0
    bus.WriteInstructionWord(TaskletPc + 6, 0x8950); // BT return
    bus.WriteInstructionWord(TaskletPc + 8, 0xDE2F); // MOV.L literal,R14
    bus.WriteInstructionWord(TaskletPc + 10, 0x61E2); // MOV.L @R14,R1
    bus.WriteInstructionWord(TaskletPc + 12, 0xD22D); // MOV.L literal,R2
    bus.WriteInstructionWord(TaskletPc + 14, 0x3210); // CMP/EQ R1,R2
    bus.WriteInstructionWord(TaskletPc + 16, 0x894B); // BT return
    bus.WriteLong(0x0600_46B0, FlagAddress);
    bus.WriteLong(0x0600_46AC, ValueAddress);
    bus.WriteLong(0x0600_46A8, 0x0000_0080);
    bus.WriteWord(FlagAddress, 0x0001);
    bus.WriteLong(ValueAddress, 0x0000_0080);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[15] = 0x0603_F000;
    AssertTrue(cpu.TryFastForwardSdramFlagTaskletDispatcherLoop(4096, out int cycles), "SDRAM flag tasklet dispatcher should fast-forward while the tasklet reports idle");
    AssertEqual(4096, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x0603_F000u, cpu.R[15]);
    AssertEqual(0x0000_0080u, cpu.R[1]);
    AssertEqual(0x0000_0080u, cpu.R[2]);
    AssertEqual(ValueAddress, cpu.R[14]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc + 2);
    cpu.R[14] = PointerAddress;
    cpu.R[15] = 0x0603_F000;
    AssertTrue(cpu.TryFastForwardSdramFlagTaskletDispatcherLoop(4096, out _), "dispatcher should fast-forward from the load slot after the literal address is in R14");
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x0603_F000u, cpu.R[15]);

    cpu.Reset(LoopPc + 2);
    cpu.R[14] = ValueAddress;
    cpu.R[15] = 0x0603_F000;
    AssertTrue(cpu.TryFastForwardSdramFlagTaskletDispatcherLoop(4096, out _), "dispatcher should repair load-slot entries left with tasklet scratch state");
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x0603_F000u, cpu.R[15]);

    bus.WriteLong(ValueAddress, 0x0000_0081);
    AssertTrue(!cpu.TryFastForwardSdramFlagTaskletDispatcherLoop(4096, out _), "dispatcher should fall back when the guarded tasklet value changes");
}

void ThirtyTwoXSh2GbrBytePairTaskletFastForward()
{
    const uint Pc = 0x0600_4B64;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(Pc + 0, 0xC42D); // MOV.B @(45,GBR),R0
    bus.WriteInstructionWord(Pc + 2, 0x6103); // MOV R0,R1
    bus.WriteInstructionWord(Pc + 4, 0xC42C); // MOV.B @(44,GBR),R0
    bus.WriteInstructionWord(Pc + 6, 0x3100); // CMP/EQ R0,R1
    bus.WriteInstructionWord(Pc + 8, 0x8944); // BT return
    bus.WriteInstructionWord(0x0600_4BF8, 0x000B); // RTS
    bus.WriteInstructionWord(0x0600_4BFA, 0x4F26); // LDS.L @R15+,PR
    bus.WriteByte(0x2000_402C, 0x3C);
    bus.WriteByte(0x2000_402D, 0x3C);
    bus.WriteLong(0x0603_DFFC, 0x0600_0658);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(Pc);
    SetSh2Property(cpu, nameof(Sh2Cpu.GBR), 0x2000_4000u);
    SetSh2Property(cpu, nameof(Sh2Cpu.PR), 0x0600_0668u);
    cpu.R[15] = 0x0603_DFFC;
    AssertTrue(cpu.TryFastForwardGbrBytePairEqualTaskletReturn(32, out int cycles), "GBR byte-pair tasklet should fast-forward when bytes match");
    AssertEqual(7, cycles);
    AssertEqual(0x0600_0668u, cpu.PC);
    AssertEqual(0x0600_0658u, cpu.PR);
    AssertEqual(0x0603_E000u, cpu.R[15]);
    AssertEqual(0x3Cu, cpu.R[0]);
    AssertEqual(0x3Cu, cpu.R[1]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(Pc);
    SetSh2Property(cpu, nameof(Sh2Cpu.GBR), 0x2000_4000u);
    SetSh2Property(cpu, nameof(Sh2Cpu.PR), 0x0600_0668u);
    cpu.R[15] = 0x0603_DFFC;
    bus.WriteByte(0x2000_402D, 0x3D);
    AssertTrue(!cpu.TryFastForwardGbrBytePairEqualTaskletReturn(32, out _), "mismatched communication bytes should fall back to the interpreter");
}

void ThirtyTwoXSh2GbrBytePairInterruptIdleLoopFastForward()
{
    const uint LoopPc = 0x0600_0660;
    const uint RoutinePc = 0x0600_4B64;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xD002); // MOV.L literal,R0
    bus.WriteInstructionWord(LoopPc + 2, 0x4F22); // STS.L PR,@-R15
    bus.WriteInstructionWord(LoopPc + 4, 0x400B); // JSR @R0
    bus.WriteInstructionWord(LoopPc + 6, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 8, 0xAFFA); // BRA loop
    bus.WriteInstructionWord(LoopPc + 10, 0x0009); // NOP
    bus.WriteLong(0x0600_066C, RoutinePc);
    bus.WriteInstructionWord(RoutinePc + 0, 0xC42D); // MOV.B @(45,GBR),R0
    bus.WriteInstructionWord(RoutinePc + 2, 0x6103); // MOV R0,R1
    bus.WriteInstructionWord(RoutinePc + 4, 0xC42C); // MOV.B @(44,GBR),R0
    bus.WriteInstructionWord(RoutinePc + 6, 0x3100); // CMP/EQ R0,R1
    bus.WriteInstructionWord(RoutinePc + 8, 0x8944); // BT return
    bus.WriteInstructionWord(0x0600_4BF8, 0x000B); // RTS
    bus.WriteInstructionWord(0x0600_4BFA, 0x4F26); // LDS.L @R15+,PR
    bus.WriteByte(0x2000_402C, 0x0D);
    bus.WriteByte(0x2000_402D, 0x0D);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc + 4);
    SetSh2Property(cpu, nameof(Sh2Cpu.GBR), 0x2000_4000u);
    SetSh2Property(cpu, nameof(Sh2Cpu.PR), 0x0600_0658u);
    cpu.R[15] = 0x0603_E000;
    AssertTrue(cpu.TryFastForwardGbrBytePairEqualInterruptIdleLoop(8192, out int cycles), "GBR byte-pair interrupt idle loop should fast-forward when communication bytes match");
    AssertEqual(8192, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0x0600_0658u, cpu.PR);
    AssertEqual(0x0603_E000u, cpu.R[15]);
    AssertEqual(0x0Du, cpu.R[0]);
    AssertEqual(0x0Du, cpu.R[1]);
    AssertEqual(1u, cpu.SR & 1);

    bus.WriteByte(0x2000_402D, 0x0E);
    AssertTrue(!cpu.TryFastForwardGbrBytePairEqualInterruptIdleLoop(8192, out _), "mismatched communication bytes should fall back to the interpreter");
}

void ThirtyTwoXSh2GbrByteZeroBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_483E;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xC420); // MOV.B @(32,GBR),R0
    bus.WriteInstructionWord(LoopPc + 2, 0x2008); // TST R0,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x89FC); // BT loop
    bus.WriteByte(0x2000_4020, 0x00);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc + 2);
    SetSh2Property(cpu, nameof(Sh2Cpu.GBR), 0x2000_4000u);
    AssertTrue(cpu.TryFastForwardGbrByteZeroTstBtPollLoop(1024, displacement: 0x20, out int cycles), "GBR byte zero BT poll should fast-forward from the TST instruction");
    AssertEqual(1024, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    bus.WriteByte(0x2000_4020, 0x80);
    AssertTrue(!cpu.TryFastForwardGbrByteZeroTstBtPollLoop(1024, displacement: 0x20, out _), "nonzero GBR byte should fall back to the interpreter");
}

void ThirtyTwoXSh2LiteralByteDisplacementTstRegisterBtPollLoopFastForward()
{
    const uint SetupPc = 0x0600_12F0;
    const uint LoadPc = SetupPc + 4;
    const uint BaseAddress = 0x2000_4100;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(SetupPc + 0, 0xDE04); // MOV.L base literal,R14
    bus.WriteInstructionWord(SetupPc + 2, 0xD103); // MOV.L mask literal,R1
    bus.WriteInstructionWord(SetupPc + 4, 0x84EA); // MOV.B @(10,R14),R0
    bus.WriteInstructionWord(SetupPc + 6, 0x2018); // TST R1,R0
    bus.WriteInstructionWord(SetupPc + 8, 0x89FC); // BT load
    bus.WriteLong(0x0600_1300, 0x0000_0080);
    bus.WriteLong(0x0600_1304, BaseAddress);
    bus.WriteByte(BaseAddress + 10, 0x00);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(SetupPc);
    AssertTrue(cpu.TryFastForwardLiteralByteDisplacementTstRegisterBtPollLoop(2048, out int cycles), "literal byte displacement TST register BT poll should fast-forward from setup");
    AssertEqual(2048, cycles);
    AssertEqual(LoadPc, cpu.PC);
    AssertEqual(BaseAddress, cpu.R[14]);
    AssertEqual(0x80u, cpu.R[1]);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoadPc + 4);
    AssertTrue(cpu.TryFastForwardLiteralByteDisplacementTstRegisterBtPollLoop(2048, out _), "literal byte displacement TST register BT poll should fast-forward from the branch instruction");
    AssertEqual(LoadPc, cpu.PC);

    bus.WriteByte(BaseAddress + 10, 0x80);
    AssertTrue(!cpu.TryFastForwardLiteralByteDisplacementTstRegisterBtPollLoop(2048, out _), "set mask bit should fall back to the interpreter");
}

void ThirtyTwoXSh2MovWordSwapCopyLoopFastForward()
{
    const uint LoopPc = 0x0600_1000;
    const uint Source = 0x0600_2000;
    const uint DestinationEnd = 0x0600_3008;
    SyntheticSh2Bus interpretedBus = new();
    LoadSwapCopyLoop(interpretedBus);
    SeedSwapCopyData(interpretedBus);
    Sh2Cpu interpreted = CreateSwapCopyCpu(interpretedBus);
    interpreted.InstructionObserver = _ => { };
    interpreted.Run(64);

    SyntheticSh2Bus fastBus = new();
    LoadSwapCopyLoop(fastBus);
    SeedSwapCopyData(fastBus);
    Sh2Cpu fast = CreateSwapCopyCpu(fastBus);
    AssertTrue(
        fast.TryFastForwardMovWPostIncSwapPreDecDtBfSLoop(256, fastBus.TryReadWord, fastBus.TryWriteWord, out int cycles),
        "MOV.W postincrement/swap/predecrement copy loop should fast-forward");
    AssertTrue(cycles > 0, "fast-forwarded copy loop should consume cycles");
    fast.Step();
    fast.Step();

    AssertEqual(interpreted.PC, fast.PC);
    AssertEqual(interpreted.R[1], fast.R[1]);
    AssertEqual(interpreted.R[4], fast.R[4]);
    AssertEqual(interpreted.R[8], fast.R[8]);
    AssertEqual(interpretedBus.ReadWord(DestinationEnd - 2), fastBus.ReadWord(DestinationEnd - 2));
    AssertEqual(interpretedBus.ReadWord(DestinationEnd - 4), fastBus.ReadWord(DestinationEnd - 4));
    AssertEqual(interpretedBus.ReadWord(DestinationEnd - 6), fastBus.ReadWord(DestinationEnd - 6));
    AssertEqual(interpretedBus.ReadWord(DestinationEnd - 8), fastBus.ReadWord(DestinationEnd - 8));

    static void LoadSwapCopyLoop(SyntheticSh2Bus bus)
    {
        bus.WriteInstructionWord(LoopPc + 0, 0x6045); // MOV.W @R4+,R0
        bus.WriteInstructionWord(LoopPc + 2, 0x4110); // DT R1
        bus.WriteInstructionWord(LoopPc + 4, 0x6008); // SWAP.B R0,R0
        bus.WriteInstructionWord(LoopPc + 6, 0x8FFB); // BF/S loop
        bus.WriteInstructionWord(LoopPc + 8, 0x2805); // MOV.W R0,@-R8
        bus.WriteInstructionWord(LoopPc + 10, 0x001B); // SLEEP
    }

    static void SeedSwapCopyData(SyntheticSh2Bus bus)
    {
        bus.WriteWord(Source + 0, 0x1122);
        bus.WriteWord(Source + 2, 0x3344);
        bus.WriteWord(Source + 4, 0x5566);
        bus.WriteWord(Source + 6, 0x7788);
    }

    static Sh2Cpu CreateSwapCopyCpu(SyntheticSh2Bus bus)
    {
        Sh2Cpu cpu = new(bus, "test");
        cpu.Reset(LoopPc);
        cpu.R[1] = 4;
        cpu.R[4] = Source;
        cpu.R[8] = DestinationEnd;
        return cpu;
    }
}

void ThirtyTwoXSh2MovWordStridedCopyLoopFastForward()
{
    const uint LoopPc = 0x0600_0C8A;
    const uint Source = 0x0600_3000;
    const uint Destination = 0x0600_5000;
    SyntheticSh2Bus interpretedBus = new();
    LoadLoop(interpretedBus);
    SeedData(interpretedBus);
    Sh2Cpu interpreted = CreateCpu(interpretedBus);
    interpreted.InstructionObserver = _ => { };
    interpreted.Run(32);

    SyntheticSh2Bus fastBus = new();
    LoadLoop(fastBus);
    SeedData(fastBus);
    Sh2Cpu fast = CreateCpu(fastBus);
    AssertTrue(
        fast.TryFastForwardMovWPostIncStoreAddRegDtBfLoop(256, fastBus.TryReadWord, fastBus.TryWriteWord, out int cycles),
        "MOV.W postincrement strided copy loop should fast-forward");
    AssertTrue(cycles > 0, "fast-forwarded strided copy loop should consume cycles");
    fast.Step();
    fast.Step();

    AssertEqual(interpreted.PC, fast.PC);
    AssertEqual(interpreted.R[0], fast.R[0]);
    AssertEqual(interpreted.R[1], fast.R[1]);
    AssertEqual(interpreted.R[10], fast.R[10]);
    AssertEqual(interpreted.R[11], fast.R[11]);
    AssertEqual(interpreted.SR & 1, fast.SR & 1);
    AssertEqual(interpretedBus.ReadWord(Destination + 0), fastBus.ReadWord(Destination + 0));
    AssertEqual(interpretedBus.ReadWord(Destination + 4), fastBus.ReadWord(Destination + 4));
    AssertEqual(interpretedBus.ReadWord(Destination + 8), fastBus.ReadWord(Destination + 8));

    static void LoadLoop(SyntheticSh2Bus bus)
    {
        bus.WriteInstructionWord(LoopPc + 0, 0x60A5); // MOV.W @R10+,R0
        bus.WriteInstructionWord(LoopPc + 2, 0x2B01); // MOV.W R0,@R11
        bus.WriteInstructionWord(LoopPc + 4, 0x3B2C); // ADD R2,R11
        bus.WriteInstructionWord(LoopPc + 6, 0x4110); // DT R1
        bus.WriteInstructionWord(LoopPc + 8, 0x8BFA); // BF loop
        bus.WriteInstructionWord(LoopPc + 10, 0x000B); // RTS
        bus.WriteInstructionWord(LoopPc + 12, 0x0009); // NOP
        bus.WriteInstructionWord(LoopPc + 0x10, 0x001B); // SLEEP
    }

    static void SeedData(SyntheticSh2Bus bus)
    {
        bus.WriteWord(Source + 0, 0x1111);
        bus.WriteWord(Source + 2, 0x2222);
        bus.WriteWord(Source + 4, 0x3333);
    }

    static Sh2Cpu CreateCpu(SyntheticSh2Bus bus)
    {
        Sh2Cpu cpu = new(bus, "test");
        cpu.Reset(LoopPc);
        cpu.R[1] = 3;
        cpu.R[2] = 4;
        cpu.R[10] = Source;
        cpu.R[11] = Destination;
        SetSh2Property(cpu, nameof(Sh2Cpu.PR), LoopPc + 0x10);
        return cpu;
    }
}

void ThirtyTwoXSh2EmptyDescriptorSpanFillFastForward()
{
    const uint LoopPc = 0x0600_28A6;
    const uint DescriptorBase = 0x0603_F200;
    const uint Destination = 0x0603_F000;
    SyntheticSh2Bus bus = new();

    LoadEmptyDescriptorSpanLoop(bus);

    bus.WriteLong(0x0600_2964, DescriptorBase);
    for (uint descriptor = 0; descriptor < 4; descriptor++)
    {
        bus.WriteLong(DescriptorBase + (descriptor * 44u) + 24u, 0xFFFF_FFFF);
    }

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.R[7] = 4;
    cpu.R[8] = Destination;
    cpu.R[9] = 0x0002_0000;
    cpu.R[10] = 0x0000_03FF;

    AssertTrue(
        cpu.TryFastForwardEmptyDescriptorSpanFillLoop(256, bus.TryWriteWord, out int cycles),
        "empty descriptor span fill should fast-forward");
    AssertEqual(24, cycles);
    AssertEqual(LoopPc + 0xAA, cpu.PC);
    AssertEqual(0u, cpu.R[7]);
    AssertEqual(Destination + 8, cpu.R[8]);
    AssertEqual((ushort)0x0200, bus.ReadWord(Destination + 0));
    AssertEqual((ushort)0x0200, bus.ReadWord(Destination + 2));
    AssertEqual((ushort)0x0200, bus.ReadWord(Destination + 4));
    AssertEqual((ushort)0x0200, bus.ReadWord(Destination + 6));

    SyntheticSh2Bus midBus = new();
    LoadEmptyDescriptorSpanLoop(midBus);

    for (uint descriptor = 0; descriptor < 4; descriptor++)
    {
        midBus.WriteLong(DescriptorBase + (descriptor * 44u) + 24u, 0xFFFF_FFFF);
    }

    Sh2Cpu midCpu = new(midBus, "test");
    midCpu.Reset(LoopPc + 6);
    midCpu.R[3] = 3;
    midCpu.R[4] = DescriptorBase + 44;
    midCpu.R[7] = 2;
    midCpu.R[8] = Destination + 0x20;
    midCpu.R[9] = 0x0002_0000;
    midCpu.R[10] = 0x0000_03FF;
    AssertTrue(
        midCpu.TryFastForwardEmptyDescriptorSpanFillLoop(256, midBus.TryWriteWord, out int midCycles),
        "empty descriptor span fill should fast-forward from the descriptor scan loop");
    AssertEqual(6, midCycles);
    AssertEqual(LoopPc, midCpu.PC);
    AssertEqual(1u, midCpu.R[7]);
    AssertEqual(Destination + 0x22, midCpu.R[8]);
    AssertEqual((ushort)0x0200, midBus.ReadWord(Destination + 0x20));

    ThirtyTwoXDevice device = new();
    device.Reset();
    device.ResetSh2(slavePc: LoopPc + 6);
    LoadEmptyDescriptorSpanDeviceLoop(device);
    WriteSh2LongForTest(device, 0x0600_2964, DescriptorBase, cpuIndex: 1);
    for (uint descriptor = 0; descriptor < 4; descriptor++)
    {
        WriteSh2LongForTest(device, DescriptorBase + (descriptor * 44u) + 24u, 0xFFFF_FFFF, cpuIndex: 1);
    }

    device.SlaveSh2.R[3] = 3;
    device.SlaveSh2.R[4] = DescriptorBase + 44;
    device.SlaveSh2.R[7] = 2;
    device.SlaveSh2.R[8] = Destination + 0x40;
    device.SlaveSh2.R[9] = 0x0002_0000;
    device.SlaveSh2.R[10] = 0x0000_03FF;
    int deviceCycles = InvokeStepSh2Cpu(device, cpuIndex: 1, cycleBudget: 256);
    AssertEqual(6, deviceCycles);
    AssertEqual(LoopPc, device.SlaveSh2.PC);
    AssertEqual(1u, device.SlaveSh2.R[7]);
    AssertEqual((byte)0x02, device.Sdram[0x3F040]);
    AssertEqual((byte)0x00, device.Sdram[0x3F041]);
    int burstCycles = InvokeStepSh2Cpu(device, cpuIndex: 1, cycleBudget: 256);
    AssertEqual(6, burstCycles);
    AssertEqual(LoopPc + 0xAA, device.SlaveSh2.PC);
    AssertEqual(0u, device.SlaveSh2.R[7]);
    AssertEqual((byte)0x02, device.Sdram[0x3F042]);
    AssertEqual((byte)0x00, device.Sdram[0x3F043]);

    ThirtyTwoXDevice tailDevice = new();
    tailDevice.Reset();
    tailDevice.ResetSh2(slavePc: LoopPc + 0xA4);
    LoadEmptyDescriptorSpanDeviceLoop(tailDevice);
    tailDevice.SlaveSh2.R[5] = 0x0000_0200;
    tailDevice.SlaveSh2.R[7] = 4;
    tailDevice.SlaveSh2.R[8] = Destination + 0x80;
    int tailCycles = InvokeStepSh2Cpu(tailDevice, cpuIndex: 1, cycleBudget: 256);
    AssertEqual(22, tailCycles);
    AssertEqual(LoopPc + 0xAA, tailDevice.SlaveSh2.PC);
    AssertEqual(0u, tailDevice.SlaveSh2.R[7]);
    AssertEqual(Destination + 0x86, tailDevice.SlaveSh2.R[8]);
    AssertEqual((byte)0x02, tailDevice.Sdram[0x3F080]);
    AssertEqual((byte)0x00, tailDevice.Sdram[0x3F081]);
    AssertEqual((byte)0x02, tailDevice.Sdram[0x3F084]);
    AssertEqual((byte)0x00, tailDevice.Sdram[0x3F085]);

    static void LoadEmptyDescriptorSpanLoop(SyntheticSh2Bus target)
    {
        target.WriteInstructionWord(LoopPc + 0x00, 0xD42F);
        target.WriteInstructionWord(LoopPc + 0x02, 0xE304);
        target.WriteInstructionWord(LoopPc + 0x04, 0x6593);
        target.WriteInstructionWord(LoopPc + 0x06, 0x5046);
        target.WriteInstructionWord(LoopPc + 0x08, 0x88FF);
        target.WriteInstructionWord(LoopPc + 0x0A, 0x893B);
        ushort[] tail =
        [
            0x4310, 0x8FBE, 0x742C, 0x4519, 0x4619, 0x655F,
            0x666F, 0x4515, 0x8901, 0xA003, 0xE501, 0x35A7,
            0x8B00, 0x65A3, 0x2851, 0x7802, 0x77FF, 0x4715,
            0x89AA
        ];

        for (int i = 0; i < tail.Length; i++)
        {
            target.WriteInstructionWord(LoopPc + 0x84 + (uint)(i * 2), tail[i]);
        }
    }

    static void LoadEmptyDescriptorSpanDeviceLoop(ThirtyTwoXDevice target)
    {
        ushort[] prologue = [0xD42F, 0xE304, 0x6593, 0x5046, 0x88FF, 0x893B];
        for (int i = 0; i < prologue.Length; i++)
        {
            WriteSh2WordForTest(target, LoopPc + (uint)(i * 2), prologue[i], cpuIndex: 1);
        }

        ushort[] tail =
        [
            0x4310, 0x8FBE, 0x742C, 0x4519, 0x4619, 0x655F,
            0x666F, 0x4515, 0x8901, 0xA003, 0xE501, 0x35A7,
            0x8B00, 0x65A3, 0x2851, 0x7802, 0x77FF, 0x4715,
            0x89AA
        ];

        for (int i = 0; i < tail.Length; i++)
        {
            WriteSh2WordForTest(target, LoopPc + 0x84 + (uint)(i * 2), tail[i], cpuIndex: 1);
        }
    }

    static void WriteSh2WordForTest(ThirtyTwoXDevice target, uint address, ushort value, int cpuIndex)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Word", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Word helper was not found");
        method.Invoke(target, [address, value, cpuIndex]);
    }

    static void WriteSh2LongForTest(ThirtyTwoXDevice target, uint address, uint value, int cpuIndex)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Long", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Long helper was not found");
        method.Invoke(target, [address, value, cpuIndex]);
    }

    static int InvokeStepSh2Cpu(ThirtyTwoXDevice target, int cpuIndex, int cycleBudget)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("StepSh2Cpu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, binder: null, types: [typeof(int), typeof(int)], modifiers: null)
            ?? throw new InvalidOperationException("StepSh2Cpu helper was not found");
        return (int)method.Invoke(target, [cpuIndex, cycleBudget])!;
    }
}

void ThirtyTwoXSh2LongDifferencePollFastForward()
{
    const uint LoopPc = 0x0600_0646;
    const uint CompletionAddress = 0x0600_318C;
    const uint SourceAddress = 0x0600_145C;
    SyntheticSh2Bus bus = new();
    ushort[] loop =
    [
        0xD235, 0x6122, 0xD233, 0x6022, 0x3018, 0x8801, 0x8BF8
    ];

    for (int i = 0; i < loop.Length; i++)
    {
        bus.WriteInstructionWord(LoopPc + (uint)(i * 2), loop[i]);
    }

    bus.WriteLong(0x0600_071C, CompletionAddress);
    bus.WriteLong(0x0600_0718, SourceAddress);
    bus.WriteLong(CompletionAddress, 0);
    bus.WriteLong(SourceAddress, 0x1B);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    AssertTrue(
        cpu.TryFastForwardLongDifferenceEqualsOnePollLoop(32, bus.TryWriteLong, out int cycles),
        "long difference poll loop should publish the completion mirror");
    AssertEqual(8, cycles);
    AssertEqual(LoopPc + 0x0E, cpu.PC);
    AssertEqual(0x1Au, bus.ReadLong(CompletionAddress));
    AssertEqual(1u, cpu.R[0]);
    AssertEqual(0x1Au, cpu.R[1]);
    AssertEqual(SourceAddress, cpu.R[2]);

    SyntheticSh2Bus matchingBus = new();
    for (int i = 0; i < loop.Length; i++)
    {
        matchingBus.WriteInstructionWord(LoopPc + (uint)(i * 2), loop[i]);
    }

    matchingBus.WriteLong(0x0600_071C, CompletionAddress);
    matchingBus.WriteLong(0x0600_0718, SourceAddress);
    matchingBus.WriteLong(CompletionAddress, 0x1A);
    matchingBus.WriteLong(SourceAddress, 0x1B);
    Sh2Cpu matchingCpu = new(matchingBus, "test");
    matchingCpu.Reset(LoopPc);
    AssertTrue(
        !matchingCpu.TryFastForwardLongDifferenceEqualsOnePollLoop(32, matchingBus.TryWriteLong, out _),
        "long difference poll loop should leave an already satisfied loop to the interpreter");
}

void ThirtyTwoXSh2FrameBufferWordFillLoopFastForward()
{
    byte[] rom = new byte[0x80];
    WriteWord(rom, 0x00, 0xD006); // MOV.L @(literal,PC),R0
    WriteWord(rom, 0x02, 0xD107); // MOV.L @(literal,PC),R1
    WriteWord(rom, 0x04, 0xE203); // MOV #3,R2
    WriteWord(rom, 0x06, 0x2101); // MOV.W R0,@R1
    WriteWord(rom, 0x08, 0x7102); // ADD #2,R1
    WriteWord(rom, 0x0A, 0x4210); // DT R2
    WriteWord(rom, 0x0C, 0x8BFB); // BF $-10 back to MOV.W
    WriteWord(rom, 0x0E, 0x001B); // SLEEP
    WriteLong(rom, 0x1C, 0x0000_0202);
    WriteLong(rom, 0x20, ThirtyTwoXHardwareProfile.Sh2FrameBufferStart + 0x100);

    ThirtyTwoXDevice device = new(rom);
    device.Reset();
    device.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    device.RestoreState(device.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        VdpAccessGrantedToSh2 = true,
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
    });

    int steps = device.RunSh2Cycles(128);
    AssertTrue(steps < 20, "framebuffer fill loop should collapse repeated MOV.W/ADD/DT/BF iterations");
    AssertTrue(device.MasterSh2.Halted, "master SH-2 should leave the collapsed fill loop");
    AssertEqual(0u, device.MasterSh2.R[2]);
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2FrameBufferStart + 0x106, device.MasterSh2.R[1]);
    AssertEqual((byte)0x02, device.DrawFrameBuffer[0x100]);
    AssertEqual((byte)0x02, device.DrawFrameBuffer[0x101]);
    AssertEqual((byte)0x02, device.DrawFrameBuffer[0x104]);
    AssertEqual((byte)0x02, device.DrawFrameBuffer[0x105]);
}

void ThirtyTwoXSh2SdramMirrors()
{
    byte[] rom = new byte[0x80];
    WriteWord(rom, 0x00, 0xD006); // MOV.L @(literal,PC),R0
    WriteWord(rom, 0x02, 0xD107); // MOV.L @(literal,PC),R1
    WriteWord(rom, 0x04, 0xD207); // MOV.L @(literal,PC),R2
    WriteWord(rom, 0x06, 0x2012); // MOV.L R1,@R0
    WriteWord(rom, 0x08, 0x6322); // MOV.L @R2,R3
    WriteWord(rom, 0x0A, 0x001B); // SLEEP
    WriteLong(rom, 0x1C, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x0001_2340);
    WriteLong(rom, 0x20, 0x1357_2468);
    WriteLong(rom, 0x24, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x0041_2340);

    ThirtyTwoXDevice device = new(rom);
    device.Reset();
    device.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    device.RestoreState(device.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
    });
    device.MasterSh2.Run(16);

    AssertEqual(0x1357_2468u, device.MasterSh2.R[3]);
}

void ThirtyTwoXSh2GbrCmpEqBfPollLoopFastForward()
{
    byte[] rom = new byte[0x40];
    WriteWord(rom, 0x00, 0xD002); // MOV.L @(2,PC),R0
    WriteWord(rom, 0x02, 0x401E); // LDC R0,GBR
    WriteWord(rom, 0x04, 0xC608); // MOV.L @(8,GBR),R0
    WriteWord(rom, 0x06, 0x8800); // CMP/EQ #0,R0
    WriteWord(rom, 0x08, 0x8BFC); // BF $-8 back to MOV.L
    WriteWord(rom, 0x0A, 0x001B); // SLEEP
    WriteLong(rom, 0x0C, ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart);

    ThirtyTwoXDevice device = new(rom);
    device.Reset();
    device.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    device.RestoreState(device.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
    });
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset, 0x0001);

    int partial = device.RunSh2Cycles(300);
    AssertTrue(partial <= 6, "GBR CMP/EQ poll loop should collapse repeated status checks");
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x04, device.MasterSh2.PC);
    AssertEqual(0x0001_0000u, device.MasterSh2.R[0]);
    AssertTrue(!device.MasterSh2.Halted, "non-zero poll value should keep the SH-2 in the loop");

    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset, 0x0000);
    device.RunSh2Cycles(80);
    AssertTrue(device.MasterSh2.Halted, "zero poll value should let the SH-2 leave the loop and sleep");
}

void ThirtyTwoXSh2GbrCmpEqBtPollLoopFastForward()
{
    const uint LoopPc = 0x0600_1BD8;
    const uint Gbr = 0x2000_4000;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xC516); // MOV.W @(44,GBR),R0
    bus.WriteInstructionWord(LoopPc + 2, 0x8800); // CMP/EQ #0,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x89FC); // BT loop
    bus.WriteWord(Gbr + 44, 0x0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    cpu.SetGbr(Gbr);
    AssertTrue(cpu.TryFastForwardGbrCmpEqBtPollLoop(500, out int cycles), "GBR CMP/EQ BT poll should fast-forward while the value matches");
    AssertEqual(500, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    cpu.Reset(LoopPc + 4);
    cpu.SetGbr(Gbr);
    AssertTrue(cpu.TryFastForwardGbrCmpEqBtPollLoop(500, out _), "GBR CMP/EQ BT poll should fast-forward from the branch instruction");
    AssertEqual(LoopPc, cpu.PC);

    bus.WriteWord(Gbr + 44, 0x0001);
    AssertTrue(!cpu.TryFastForwardGbrCmpEqBtPollLoop(500, out _), "nonmatching GBR CMP/EQ BT value should fall back to the interpreter");
}

void ThirtyTwoXSh2GbrRegisterCmpEqBfPollLoopFastForward()
{
    const uint LoopPc = 0x0600_01A4;
    const uint Gbr = 0x2000_4000;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xC510); // MOV.W @(32,GBR),R0
    bus.WriteInstructionWord(LoopPc + 2, 0x3100); // CMP/EQ R0,R1
    bus.WriteInstructionWord(LoopPc + 4, 0x8BFC); // BF loop
    bus.WriteWord(Gbr + 32, 0x0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    SetSh2Property(cpu, nameof(Sh2Cpu.GBR), Gbr);
    cpu.R[1] = 0x0000_534D;
    AssertTrue(cpu.TryFastForwardGbrRegisterCmpEqBfPollLoop(500, out int cycles), "GBR register compare poll should fast-forward while values differ");
    AssertEqual(500, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(0u, cpu.SR & 1);

    bus.WriteWord(Gbr + 32, 0x534D);
    AssertTrue(!cpu.TryFastForwardGbrRegisterCmpEqBfPollLoop(500, out _), "matching GBR register compare poll should fall back and leave normally");
}

void ThirtyTwoXSh2NullLinkedListIdleLoopFastForward()
{
    const uint LoopPc = 0x0600_4588;
    const uint ListRoot = 0x2600_4A84;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0x00, 0xDD0B); // MOV.L @(literal,PC),R13
    bus.WriteInstructionWord(LoopPc + 0x02, 0x6ED2); // MOV.L @R13,R14
    bus.WriteInstructionWord(LoopPc + 0x04, 0x5DD1); // MOV.L @(4,R13),R13
    bus.WriteInstructionWord(LoopPc + 0x06, 0x2EE8); // TST R14,R14
    bus.WriteInstructionWord(LoopPc + 0x08, 0x8F08); // BF/S work
    bus.WriteInstructionWord(LoopPc + 0x0A, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x0C, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x0E, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x10, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x12, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x14, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x16, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x18, 0xAFF2); // BRA loop
    bus.WriteInstructionWord(LoopPc + 0x1A, 0x0009); // NOP
    bus.WriteLong(LoopPc + 0x30, ListRoot);
    bus.WriteLong(ListRoot, 0);
    bus.WriteLong(ListRoot + 4, 0xFFFF_FFFF);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    AssertTrue(cpu.TryFastForwardSdramNullLinkedListIdleLoop(1024, bus.TryReadLong, out int cycles), "null linked-list idle loop should fast-forward while no work is queued");
    AssertEqual(1024, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(ListRoot, cpu.R[13]);
    AssertEqual(0u, cpu.R[14]);
    AssertEqual(1u, cpu.SR & 1);

    bus.WriteLong(ListRoot, 0x2600_5000);
    AssertTrue(!cpu.TryFastForwardSdramNullLinkedListIdleLoop(1024, bus.TryReadLong, out _), "queued linked-list work should fall back to the interpreter");
}

void ThirtyTwoXSh2GbrWordCmpGtBfPollLoopFastForward()
{
    const uint LoopPc = 0x0600_0370;
    const uint Gbr = 0x2000_4000;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0, 0xC510); // MOV.W @(32,GBR),R0
    bus.WriteInstructionWord(LoopPc + 2, 0x3017); // CMP/GT R1,R0
    bus.WriteInstructionWord(LoopPc + 4, 0x8BFC); // BF loop
    bus.WriteWord(Gbr + 32, 0x0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    SetSh2Property(cpu, nameof(Sh2Cpu.GBR), Gbr);
    cpu.R[1] = 1;
    AssertTrue(cpu.TryFastForwardGbrWordCmpGtBfPollLoop(500, out int cycles), "GBR word compare-greater poll should fast-forward while the branch remains taken");
    AssertEqual(500, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(0u, cpu.SR & 1);

    cpu.Reset(LoopPc + 2);
    SetSh2Property(cpu, nameof(Sh2Cpu.GBR), Gbr);
    cpu.R[1] = 1;
    AssertTrue(cpu.TryFastForwardGbrWordCmpGtBfPollLoop(500, out _), "GBR word compare-greater poll should fast-forward from the compare instruction");
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);

    const uint SetupPc = 0x0600_046E;
    SyntheticSh2Bus setupBus = new();
    setupBus.WriteInstructionWord(SetupPc + 0, 0xE100); // MOV #0,R1
    setupBus.WriteInstructionWord(SetupPc + 2, 0xC510); // MOV.W @(32,GBR),R0
    setupBus.WriteInstructionWord(SetupPc + 4, 0x3017); // CMP/GT R1,R0
    setupBus.WriteInstructionWord(SetupPc + 6, 0x8BFB); // BF setup
    setupBus.WriteWord(Gbr + 32, 0x0000);
    Sh2Cpu setupCpu = new(setupBus, "test");
    setupCpu.Reset(SetupPc + 2);
    SetSh2Property(setupCpu, nameof(Sh2Cpu.GBR), Gbr);
    setupCpu.R[1] = 9;
    AssertTrue(setupCpu.TryFastForwardGbrWordCmpGtBfPollLoop(500, out _), "GBR word compare-greater poll should fast-forward through a repeated immediate setup");
    AssertEqual(SetupPc, setupCpu.PC);
    AssertEqual(0u, setupCpu.R[1]);

    setupCpu.Reset(SetupPc);
    SetSh2Property(setupCpu, nameof(Sh2Cpu.GBR), Gbr);
    setupCpu.R[1] = 9;
    AssertTrue(setupCpu.TryFastForwardGbrWordCmpGtBfPollLoop(500, out _), "GBR word compare-greater poll should fast-forward when entered at the immediate setup");
    AssertEqual(SetupPc, setupCpu.PC);
    AssertEqual(0u, setupCpu.R[1]);

    bus.WriteWord(Gbr + 32, 0x0002);
    AssertTrue(!cpu.TryFastForwardGbrWordCmpGtBfPollLoop(500, out _), "greater GBR word should fall back and let the loop exit normally");
}

void ThirtyTwoXSh2PaddedGbrCmpEqBfBraPollLoopFastForward()
{
    const uint LoopPc = 0x0600_0100;
    const uint Gbr = 0x2000_4000;
    SyntheticSh2Bus bus = new();
    bus.WriteInstructionWord(LoopPc + 0x00, 0xC536); // MOV.W @(108,GBR),R0
    bus.WriteInstructionWord(LoopPc + 0x02, 0x8800); // CMP/EQ #0,R0
    bus.WriteInstructionWord(LoopPc + 0x04, 0x8B07); // BF exit
    bus.WriteInstructionWord(LoopPc + 0x06, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x08, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x0A, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x0C, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x0E, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x10, 0x0009); // NOP
    bus.WriteInstructionWord(LoopPc + 0x12, 0xAFF5); // BRA loop
    bus.WriteInstructionWord(LoopPc + 0x14, 0x0009); // NOP
    bus.WriteWord(Gbr + 108, 0x0000);

    Sh2Cpu cpu = new(bus, "test");
    cpu.Reset(LoopPc);
    SetSh2Property(cpu, nameof(Sh2Cpu.GBR), Gbr);
    AssertTrue(cpu.TryFastForwardGbrCmpEqBfBraPollLoop(500, out int cycles), "padded GBR zero poll should fast-forward");
    AssertEqual(500, cycles);
    AssertEqual(LoopPc, cpu.PC);
    AssertEqual(0u, cpu.R[0]);
    AssertEqual(1u, cpu.SR & 1);

    bus.WriteWord(Gbr + 108, 0x0001);
    AssertTrue(!cpu.TryFastForwardGbrCmpEqBfBraPollLoop(500, out _), "non-zero padded poll value should fall back and exit normally");
}

void ThirtyTwoXSh2LinkedListInsertFastForwardMatchesInterpreter()
{
    const uint LoopPc = 0x0603_040C;
    const uint RechainPc = 0x0603_044C;
    const uint ReturnPc = 0x0603_06B0;
    const uint Root = 0x0600_0000;
    const uint Existing = 0x0600_0100;
    const uint NewNode = 0x0600_0200;
    const uint RechainTail = 0x0600_0300;
    const uint Threshold = 0x0000_0800;

    static void LoadSdkInsertRoutine(SyntheticSh2Bus bus, uint basePc)
    {
        ushort[] opcodes =
        [
            0x5312, // MOV.L @(8,R1),R3
            0x6213, // MOV R1,R2
            0x5111, // MOV.L @(4,R1),R1
            0x3303, // CMP/GE R0,R3
            0x8904, // BT exit
            0x5312, // MOV.L @(8,R1),R3
            0x6213, // MOV R1,R2
            0x5111, // MOV.L @(4,R1),R1
            0x3303, // CMP/GE R0,R3
            0x8BF5, // BF loop
            0x5120, // MOV.L @(0,R2),R1
            0x1410, // MOV.L R1,@(0,R4)
            0x1421, // MOV.L R2,@(4,R4)
            0x1141, // MOV.L R4,@(4,R1)
            0x000B, // RTS
            0x1240, // MOV.L R4,@R2
        ];

        for (int i = 0; i < opcodes.Length; i++)
        {
            bus.WriteInstructionWord(basePc + (uint)(i * 2), opcodes[i]);
        }

        bus.WriteInstructionWord(basePc + (uint)(opcodes.Length * 2), 0x0009);
    }

    static void LoadSdkRechainRoutine(SyntheticSh2Bus bus, uint basePc)
    {
        ushort[] opcodes =
        [
            0x5632, // MOV.L @(8,R3),R6
            0x5431, // MOV.L @(4,R3),R4
            0x3963, // CMP/GE R6,R9
            0x5130, // MOV.L @(0,R3),R1
            0x1141, // MOV.L R4,@(4,R1)
            0x1410, // MOV.L R1,@(0,R4)
            0x8905, // BT insert
            0x0009, // NOP
            0x6783, // MOV R8,R7
            0x5881, // MOV.L @(4,R8),R8
            0x5982, // MOV.L @(8,R8),R9
            0x3967, // CMP/GE R6,R9
            0x8BFA, // BF walk
            0x1731, // MOV.L R3,@(4,R7)
            0x1830, // MOV.L R3,@(0,R8)
            0x1370, // MOV.L R7,@(0,R3)
            0x6733, // MOV R3,R7
            0x1381, // MOV.L R8,@(4,R3)
            0x3540, // CMP/EQ R4,R5
            0x8FEB, // BF/S loop
            0x6343, // MOV R4,R3
            0x000B, // RTS
        ];

        for (int i = 0; i < opcodes.Length; i++)
        {
            bus.WriteInstructionWord(basePc + (uint)(i * 2), opcodes[i]);
        }

        bus.WriteInstructionWord(basePc + (uint)(opcodes.Length * 2), 0x0009);
    }

    static void SeedList(SyntheticSh2Bus bus)
    {
        bus.WriteLong(Root, Existing);
        bus.WriteLong(Root + 4, Existing);
        bus.WriteLong(Root + 8, 0);
        bus.WriteLong(Existing, Root);
        bus.WriteLong(Existing + 4, Root);
        bus.WriteLong(Existing + 8, 0x0000_1000);
        bus.WriteLong(NewNode, 0);
        bus.WriteLong(NewNode + 4, 0);
        bus.WriteLong(NewNode + 8, Threshold);
    }

    static void SeedRechainList(SyntheticSh2Bus bus)
    {
        bus.WriteLong(Root, RechainTail);
        bus.WriteLong(Root + 4, Existing);
        bus.WriteLong(Root + 8, 0);
        bus.WriteLong(Existing, Root);
        bus.WriteLong(Existing + 4, RechainTail);
        bus.WriteLong(Existing + 8, 0x0000_0100);
        bus.WriteLong(RechainTail, Existing);
        bus.WriteLong(RechainTail + 4, Root);
        bus.WriteLong(RechainTail + 8, 0x0000_0200);
    }

    static Sh2Cpu CreateCpu(SyntheticSh2Bus bus)
    {
        Sh2Cpu cpu = new(bus, "test");
        cpu.Reset(LoopPc);
        SetSh2Property(cpu, nameof(Sh2Cpu.PR), ReturnPc);
        cpu.R[0] = Threshold;
        cpu.R[1] = Existing;
        cpu.R[2] = Root;
        cpu.R[3] = 0;
        cpu.R[4] = NewNode;
        return cpu;
    }

    SyntheticSh2Bus interpretedBus = new();
    LoadSdkInsertRoutine(interpretedBus, LoopPc);
    interpretedBus.WriteInstructionWord(ReturnPc, 0x001B);
    SeedList(interpretedBus);
    Sh2Cpu interpreted = CreateCpu(interpretedBus);
    interpreted.InstructionObserver = _ => { };
    interpreted.Run(64);

    SyntheticSh2Bus fastBus = new();
    LoadSdkInsertRoutine(fastBus, LoopPc);
    SeedList(fastBus);
    Sh2Cpu fast = CreateCpu(fastBus);
    bool accelerated = fast.TryFastForwardSdramLinkedListInsertRoutine(
        1000,
        fastBus.TryReadLong,
        fastBus.TryWriteLong,
        observer: null,
        out int cycles);

    AssertTrue(accelerated, "valid SDK linked-list insert should fast-forward");
    AssertTrue(cycles > 0, "fast-forwarded insert should consume cycles");
    AssertEqual(ReturnPc, fast.PC);
    AssertEqual(interpreted.R[1], fast.R[1]);
    AssertEqual(interpreted.R[2], fast.R[2]);
    AssertEqual(interpretedBus.ReadLong(Root), fastBus.ReadLong(Root));
    AssertEqual(interpretedBus.ReadLong(Root + 4), fastBus.ReadLong(Root + 4));
    AssertEqual(interpretedBus.ReadLong(Existing), fastBus.ReadLong(Existing));
    AssertEqual(interpretedBus.ReadLong(NewNode), fastBus.ReadLong(NewNode));
    AssertEqual(interpretedBus.ReadLong(NewNode + 4), fastBus.ReadLong(NewNode + 4));

    SyntheticSh2Bus invalidBus = new();
    LoadSdkInsertRoutine(invalidBus, LoopPc);
    SeedList(invalidBus);
    Sh2Cpu invalid = CreateCpu(invalidBus);
    invalid.Reset(LoopPc + 16);
    SetSh2Property(invalid, nameof(Sh2Cpu.PR), ReturnPc);
    invalid.R[0] = 0x5797_FFFF;
    invalid.R[1] = 0xE002_2080;
    invalid.R[2] = 0x0600_0808;
    invalid.R[3] = 0x0C03_2080;
    invalid.R[4] = NewNode;
    AssertTrue(
        invalid.TryFastForwardSdramLinkedListInsertRoutine(1000, invalidBus.TryReadLong, invalidBus.TryWriteLong, observer: null, out _),
        "Runlength SDK invalid linked-list cursor should complete as a bounded no-op");
    AssertEqual(ReturnPc, invalid.PC);
    AssertEqual(0u, invalidBus.ReadLong(NewNode));
    AssertEqual(0u, invalidBus.ReadLong(NewNode + 4));

    const uint RunlengthSdkLoopPc = 0x0603_040C;
    SyntheticSh2Bus runlengthInvalidBus = new();
    LoadSdkInsertRoutine(runlengthInvalidBus, RunlengthSdkLoopPc);
    SeedList(runlengthInvalidBus);
    Sh2Cpu runlengthInvalid = CreateCpu(runlengthInvalidBus);
    runlengthInvalid.Reset(RunlengthSdkLoopPc + 16);
    SetSh2Property(runlengthInvalid, nameof(Sh2Cpu.PR), ReturnPc);
    runlengthInvalid.R[0] = 0x5797_FFFF;
    runlengthInvalid.R[1] = 0x0000_0003;
    runlengthInvalid.R[2] = 0x0600_0808;
    runlengthInvalid.R[3] = 0;
    runlengthInvalid.R[4] = NewNode;
    AssertTrue(
        runlengthInvalid.TryFastForwardSdramLinkedListInsertRoutine(1000, runlengthInvalidBus.TryReadLong, runlengthInvalidBus.TryWriteLong, observer: null, out int invalidCycles),
        "Runlength SDK invalid linked-list cursor should be treated as a bounded no-op");
    AssertTrue(invalidCycles > 0, "Runlength SDK invalid no-op should consume cycles");
    AssertEqual(ReturnPc, runlengthInvalid.PC);
    AssertEqual(0u, runlengthInvalidBus.ReadLong(NewNode));
    AssertEqual(0u, runlengthInvalidBus.ReadLong(NewNode + 4));

    SyntheticSh2Bus currentIsNewNodeBus = new();
    LoadSdkInsertRoutine(currentIsNewNodeBus, LoopPc);
    SeedList(currentIsNewNodeBus);
    Sh2Cpu currentIsNewNode = CreateCpu(currentIsNewNodeBus);
    currentIsNewNode.Reset(LoopPc + 28);
    SetSh2Property(currentIsNewNode, nameof(Sh2Cpu.PR), ReturnPc);
    currentIsNewNode.R[1] = Existing;
    currentIsNewNode.R[2] = NewNode;
    currentIsNewNode.R[4] = NewNode;
    AssertTrue(
        !currentIsNewNode.TryFastForwardSdramLinkedListInsertRoutine(1000, currentIsNewNodeBus.TryReadLong, currentIsNewNodeBus.TryWriteLong, observer: null, out _),
        "linked-list insert should not fast-forward when the insertion cursor is the new node itself");

    SyntheticSh2Bus interpretedRechainBus = new();
    LoadSdkRechainRoutine(interpretedRechainBus, RechainPc);
    interpretedRechainBus.WriteInstructionWord(ReturnPc, 0x001B);
    SeedRechainList(interpretedRechainBus);
    Sh2Cpu interpretedRechain = new(interpretedRechainBus, "test");
    interpretedRechain.Reset(RechainPc);
    SetSh2Property(interpretedRechain, nameof(Sh2Cpu.PR), ReturnPc);
    interpretedRechain.R[3] = Existing;
    interpretedRechain.R[5] = RechainTail;
    interpretedRechain.R[7] = Root;
    interpretedRechain.R[8] = RechainTail;
    interpretedRechain.R[9] = 0x0000_0200;
    interpretedRechain.InstructionObserver = _ => { };
    interpretedRechain.Run(64);

    SyntheticSh2Bus fastRechainBus = new();
    LoadSdkRechainRoutine(fastRechainBus, RechainPc);
    SeedRechainList(fastRechainBus);
    Sh2Cpu fastRechain = new(fastRechainBus, "test");
    fastRechain.Reset(RechainPc);
    SetSh2Property(fastRechain, nameof(Sh2Cpu.PR), ReturnPc);
    fastRechain.R[3] = Existing;
    fastRechain.R[5] = RechainTail;
    fastRechain.R[7] = Root;
    fastRechain.R[8] = RechainTail;
    fastRechain.R[9] = 0x0000_0200;
    AssertTrue(
        fastRechain.TryFastForwardRunlengthSdkRechainRoutine(1000, fastRechainBus.TryReadLong, fastRechainBus.TryWriteLong, observer: null, out int rechainCycles),
        "Runlength SDK rechain routine should fast-forward valid short lists");
    AssertTrue(rechainCycles > 0, "Runlength SDK rechain fast-forward should consume cycles");
    AssertEqual(ReturnPc, fastRechain.PC);
    AssertEqual(interpretedRechain.R[3], fastRechain.R[3]);
    AssertEqual(interpretedRechain.R[4], fastRechain.R[4]);
    AssertEqual(interpretedRechain.R[7], fastRechain.R[7]);
    AssertEqual(interpretedRechainBus.ReadLong(Root), fastRechainBus.ReadLong(Root));
    AssertEqual(interpretedRechainBus.ReadLong(Root + 4), fastRechainBus.ReadLong(Root + 4));
    AssertEqual(interpretedRechainBus.ReadLong(Existing), fastRechainBus.ReadLong(Existing));
    AssertEqual(interpretedRechainBus.ReadLong(Existing + 4), fastRechainBus.ReadLong(Existing + 4));
    AssertEqual(interpretedRechainBus.ReadLong(RechainTail), fastRechainBus.ReadLong(RechainTail));
    AssertEqual(interpretedRechainBus.ReadLong(RechainTail + 4), fastRechainBus.ReadLong(RechainTail + 4));
}

void ThirtyTwoXSh2DmaTransferSizeBits()
{
    static void WriteSh2ByteForTest(ThirtyTwoXDevice target, uint address, byte value)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Byte helper was not found");
        method.Invoke(target, [address, value, 0]);
    }

    static void WriteSh2LongForTest(ThirtyTwoXDevice target, uint address, uint value)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Long", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Long helper was not found");
        method.Invoke(target, [address, value, 0]);
    }

    byte[] rom = new byte[0x180];
    WriteWord(rom, 0xC0, 0x1234);
    WriteWord(rom, 0xC2, 0x5678);
    WriteWord(rom, 0xC4, 0x9ABC);
    WriteWord(rom, 0xC6, 0xDEF0);

    int pc = 0;
    int literal = 0x80;
    void EmitMovLongLiteral(int register, uint value)
    {
        WriteLong(rom, literal, value);
        int baseAddress = (pc + 4) & ~3;
        int displacement = (literal - baseAddress) / 4;
        WriteWord(rom, pc, (ushort)(0xD000 | (register << 8) | displacement));
        pc += 2;
        literal += 4;
    }

    void EmitStoreR1AtR0()
    {
        WriteWord(rom, pc, 0x2012); // MOV.L R1,@R0
        pc += 2;
    }

    EmitMovLongLiteral(0, 0xFFFF_FF80);
    EmitMovLongLiteral(1, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0xC0);
    EmitStoreR1AtR0();
    EmitMovLongLiteral(0, 0xFFFF_FF84);
    EmitMovLongLiteral(1, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x100);
    EmitStoreR1AtR0();
    EmitMovLongLiteral(0, 0xFFFF_FF88);
    EmitMovLongLiteral(1, 2);
    EmitStoreR1AtR0();
    EmitMovLongLiteral(0, 0xFFFF_FF8C);
    EmitMovLongLiteral(1, 0x0000_5611); // Word-sized auto-request DMA with source/destination increment.
    EmitStoreR1AtR0();
    EmitMovLongLiteral(0, 0xFFFF_FFB0);
    EmitMovLongLiteral(1, 1);
    EmitStoreR1AtR0();
    WriteWord(rom, pc, 0x001B);

    ThirtyTwoXDevice device = new(rom);
    device.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    device.MasterSh2.Run(80);

    AssertEqual((byte)0x12, device.Sdram[0x100]);
    AssertEqual((byte)0x34, device.Sdram[0x101]);
    AssertEqual((byte)0x56, device.Sdram[0x102]);
    AssertEqual((byte)0x78, device.Sdram[0x103]);
    AssertEqual((byte)0x00, device.Sdram[0x104]);

    ThirtyTwoXDevice interruptDevice = new(rom);
    interruptDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    WriteSh2ByteForTest(interruptDevice, 0xFFFF_FEE2, 0x07); // DMAC priority = 7 in IPRA bits 11-8.
    WriteSh2LongForTest(interruptDevice, 0xFFFF_FFA0, 0x0000_0055); // VCRDMA0 vector.
    WriteSh2LongForTest(interruptDevice, 0xFFFF_FF80, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0xC0);
    WriteSh2LongForTest(interruptDevice, 0xFFFF_FF84, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x120);
    WriteSh2LongForTest(interruptDevice, 0xFFFF_FF88, 1);
    WriteSh2LongForTest(interruptDevice, 0xFFFF_FFB0, 1);
    WriteSh2LongForTest(interruptDevice, 0xFFFF_FF8C, 0x0000_5615); // Word transfer, incrementing source/destination, IE + DE.

    AssertEqual((byte)0x12, interruptDevice.Sdram[0x120]);
    AssertEqual((byte)0x34, interruptDevice.Sdram[0x121]);
    AssertEqual(7, interruptDevice.MasterSh2.PendingInterruptLevel);
    AssertEqual(0x55, interruptDevice.MasterSh2.PendingInterruptVectorNumber);
    WriteSh2LongForTest(interruptDevice, 0xFFFF_FF8C, 0x0000_5614); // Clear TE, preserve IE and transfer mode.
    AssertEqual(0, interruptDevice.MasterSh2.PendingInterruptLevel);
    AssertEqual(0, interruptDevice.MasterSh2.PendingInterruptVectorNumber);
}

void ThirtyTwoXSh2ArithmeticFlags()
{
    byte[] rom = new byte[0x100];
    WriteWord(rom, 0x00, 0xE0FF); // MOV #-1,R0
    WriteWord(rom, 0x02, 0xE101); // MOV #1,R1
    WriteWord(rom, 0x04, 0x3107); // DIV0S R0,R1
    WriteWord(rom, 0x06, 0x0229); // MOVT R2
    WriteWord(rom, 0x08, 0x311E); // ADDC R1,R1
    WriteWord(rom, 0x0A, 0x0329); // MOVT R3
    WriteWord(rom, 0x0C, 0x351A); // SUBC R1,R5
    WriteWord(rom, 0x0E, 0x0729); // MOVT R7
    WriteWord(rom, 0x10, 0xE87F); // MOV #$7F,R8
    WriteWord(rom, 0x12, 0x688C); // EXTU.B R8,R8
    WriteWord(rom, 0x14, 0x4818); // SHLL8 R8
    WriteWord(rom, 0x16, 0x4818); // SHLL8 R8
    WriteWord(rom, 0x18, 0x4818); // SHLL8 R8 => $7F000000
    WriteWord(rom, 0x1A, 0x6983); // MOV R8,R9
    WriteWord(rom, 0x1C, 0x398F); // ADDV R8,R9
    WriteWord(rom, 0x1E, 0x0A29); // MOVT R10
    WriteWord(rom, 0x20, 0xEB80); // MOV #$80,R11
    WriteWord(rom, 0x22, 0x6BBC); // EXTU.B R11,R11
    WriteWord(rom, 0x24, 0x4B18); // SHLL8 R11
    WriteWord(rom, 0x26, 0x4B18); // SHLL8 R11
    WriteWord(rom, 0x28, 0x4B18); // SHLL8 R11 => $80000000
    WriteWord(rom, 0x2A, 0xEC01); // MOV #1,R12
    WriteWord(rom, 0x2C, 0x3BCB); // SUBV R12,R11
    WriteWord(rom, 0x2E, 0x0D29); // MOVT R13
    WriteWord(rom, 0x30, 0x03B2); // STC R3_BANK,R3
    WriteWord(rom, 0x32, 0x4BCE); // LDC R11,R4_BANK
    WriteWord(rom, 0x34, 0x03C2); // STC R4_BANK,R3
    WriteWord(rom, 0x36, 0x001B); // SLEEP

    ThirtyTwoXDevice device = new(rom);
    device.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    device.MasterSh2.Run(32);

    AssertEqual(1u, device.MasterSh2.R[2]);
    AssertEqual(0xFFFF_FFFDu, device.MasterSh2.R[5]);
    AssertEqual(1u, device.MasterSh2.R[7]);
    AssertEqual(0xFE00_0000u, device.MasterSh2.R[9]);
    AssertEqual(1u, device.MasterSh2.R[10]);
    AssertEqual(0x7FFF_FFFFu, device.MasterSh2.R[11]);
    AssertEqual(1u, device.MasterSh2.R[13]);
    AssertEqual(0x7FFF_FFFFu, device.MasterSh2.R[3]);
    AssertEqual(0x7FFF_FFFFu, device.MasterSh2.BankedR[4]);
    AssertTrue(device.MasterSh2.Halted, "SH-2 arithmetic flag synthetic program should halt");

    SyntheticSh2Bus div1Bus = new();
    div1Bus.WriteInstructionWord(0, 0x3124); // DIV1 R2,R1
    Sh2Cpu div1Cpu = new(div1Bus, "div1");
    div1Cpu.Reset();
    div1Cpu.R[1] = 0x8000_0000u;
    div1Cpu.R[2] = 0x0000_0001u;
    div1Cpu.Run(1);
    AssertEqual(0xFFFF_FFFFu, div1Cpu.R[1]);
    AssertEqual(0x0000_0001u, div1Cpu.SR & 0x101u);

    SyntheticSh2Bus div0uBus = new();
    div0uBus.WriteInstructionWord(0, 0x0019); // DIV0U
    Sh2Cpu div0uCpu = new(div0uBus, "div0u");
    div0uCpu.Reset();
    SetSh2Property(div0uCpu, nameof(Sh2Cpu.SR), 0x0000_03F1u);
    div0uCpu.Run(1);
    AssertEqual(0x0000_00F0u, div0uCpu.SR);

    SyntheticSh2Bus xtrctBus = new();
    xtrctBus.WriteInstructionWord(0, 0x201D); // XTRCT R1,R0
    Sh2Cpu xtrctCpu = new(xtrctBus, "xtrct");
    xtrctCpu.Reset();
    xtrctCpu.R[0] = 0x1122_3344u;
    xtrctCpu.R[1] = 0xAABB_CCDDu;
    xtrctCpu.Run(1);
    AssertEqual(0xCCDD_1122u, xtrctCpu.R[0]);

    byte[] tstImmediateRom = new byte[0x20];
    WriteWord(tstImmediateRom, 0x00, 0xE000); // MOV #0,R0
    WriteWord(tstImmediateRom, 0x02, 0xC802); // TST #2,R0
    WriteWord(tstImmediateRom, 0x04, 0x0029); // MOVT R0
    WriteWord(tstImmediateRom, 0x06, 0x001B); // SLEEP
    ThirtyTwoXDevice tstImmediateDevice = new(tstImmediateRom);
    tstImmediateDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    tstImmediateDevice.MasterSh2.Run(8);
    AssertEqual(1u, tstImmediateDevice.MasterSh2.R[0]);

    WriteWord(tstImmediateRom, 0x00, 0xE002); // MOV #2,R0
    tstImmediateDevice = new ThirtyTwoXDevice(tstImmediateRom);
    tstImmediateDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    tstImmediateDevice.MasterSh2.Run(8);
    AssertEqual(0u, tstImmediateDevice.MasterSh2.R[0]);

    byte[] cmpImmediateRom = new byte[0x20];
    WriteWord(cmpImmediateRom, 0x00, 0xE080); // MOV #-128,R0
    WriteWord(cmpImmediateRom, 0x02, 0x8880); // CMP/EQ #-128,R0
    WriteWord(cmpImmediateRom, 0x04, 0x0129); // MOVT R1
    WriteWord(cmpImmediateRom, 0x06, 0xE07F); // MOV #127,R0
    WriteWord(cmpImmediateRom, 0x08, 0x887F); // CMP/EQ #127,R0
    WriteWord(cmpImmediateRom, 0x0A, 0x0229); // MOVT R2
    WriteWord(cmpImmediateRom, 0x0C, 0xC880); // TST #$80,R0, remains distinct from CMP/EQ
    WriteWord(cmpImmediateRom, 0x0E, 0x0329); // MOVT R3
    WriteWord(cmpImmediateRom, 0x10, 0x001B); // SLEEP
    ThirtyTwoXDevice cmpImmediateDevice = new(cmpImmediateRom);
    cmpImmediateDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    cmpImmediateDevice.MasterSh2.Run(16);
    AssertEqual(1u, cmpImmediateDevice.MasterSh2.R[1]);
    AssertEqual(1u, cmpImmediateDevice.MasterSh2.R[2]);
    AssertEqual(1u, cmpImmediateDevice.MasterSh2.R[3]);

    byte[] indexedDisplacementRom = new byte[0x80];
    WriteWord(indexedDisplacementRom, 0x00, 0xD10A); // MOV.L @(literal,PC),R1
    WriteWord(indexedDisplacementRom, 0x02, 0xE07B); // MOV #$7B,R0
    WriteWord(indexedDisplacementRom, 0x04, 0x8119); // MOV.W R0,@(9,R1), uses 4-bit displacement
    WriteWord(indexedDisplacementRom, 0x06, 0xE000); // MOV #0,R0
    WriteWord(indexedDisplacementRom, 0x08, 0x8519); // MOV.W @(9,R1),R0
    WriteWord(indexedDisplacementRom, 0x0A, 0x001B); // SLEEP
    WriteLong(indexedDisplacementRom, 0x2C, ThirtyTwoXHardwareProfile.Sh2SdramStart + 0x20);
    ThirtyTwoXDevice indexedDisplacementDevice = new(indexedDisplacementRom);
    indexedDisplacementDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    indexedDisplacementDevice.MasterSh2.Run(16);
    AssertEqual(0x0000_007Bu, indexedDisplacementDevice.MasterSh2.R[0]);
    AssertEqual((byte)0x7B, indexedDisplacementDevice.Sdram[0x33]);
    AssertEqual((byte)0x00, indexedDisplacementDevice.Sdram[0x53]);
}

void ThirtyTwoXUserHeaderLoadsInitialProgram()
{
    byte[] rom = new byte[0x10000];
    WriteAscii(rom, 0x3C0, "MARS TEST HEADER");
    WriteLong(rom, 0x3D0, 0x0000_0001);
    WriteLong(rom, 0x3D4, 0x0000_5000);
    WriteLong(rom, 0x3D8, 0x0000_0100);
    WriteLong(rom, 0x3DC, 0x0000_0020);
    WriteLong(rom, 0x3E0, 0x0000_0100);
    WriteLong(rom, 0x3E4, 0x0600_0120);
    WriteLong(rom, 0x3E8, 0x0600_0000);
    WriteLong(rom, 0x3EC, 0x0600_0400);
    WriteWord(rom, 0x5000, 0xE177); // MOV #$77,R1
    WriteWord(rom, 0x5002, 0x001B); // SLEEP

    ThirtyTwoXDevice device = new(rom);
    device.Reset();

    AssertTrue(device.UserHeader.IsValid, "MARS user header should be parsed");
    AssertEqual(0x0000_0001u, device.UserHeader.Version);
    AssertEqual(0x0600_0100u, device.MasterSh2.PC);
    AssertEqual(0x0600_0120u, device.SlaveSh2.PC);
    AssertEqual(0x0600_0000u, device.MasterSh2.VBR);
    AssertEqual(0x0600_0400u, device.SlaveSh2.VBR);
    AssertEqual((byte)0xE1, device.Sdram[0x100]);
    AssertEqual((byte)0x77, device.Sdram[0x101]);
    AssertEqual((byte)0x00, device.Sdram[0x102]);
    AssertEqual((byte)0x1B, device.Sdram[0x103]);

    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    device.MasterSh2.Run(4);
    AssertEqual(0x0000_0077u, device.MasterSh2.R[1]);
    AssertTrue(device.MasterSh2.Halted, "loaded initial program should execute from SDRAM");

    byte[] titleHeaderRom = new byte[0x10000];
    WriteAscii(titleHeaderRom, 0x3C0, "STAR WARS ARCADE");
    WriteLong(titleHeaderRom, 0x3D0, 0x0000_0000);
    WriteLong(titleHeaderRom, 0x3D4, 0x0000_5000);
    WriteLong(titleHeaderRom, 0x3D8, 0x0000_0000);
    WriteLong(titleHeaderRom, 0x3DC, 0x0000_0020);
    WriteLong(titleHeaderRom, 0x3E0, 0x0600_0400);
    WriteLong(titleHeaderRom, 0x3E4, 0x0600_0402);
    WriteLong(titleHeaderRom, 0x3E8, 0x0600_0000);
    WriteLong(titleHeaderRom, 0x3EC, 0x0600_0200);
    WriteWord(titleHeaderRom, 0x5000, 0xE188); // MOV #$88,R1
    WriteWord(titleHeaderRom, 0x5002, 0x001B); // SLEEP

    ThirtyTwoXDevice titleHeaderDevice = new(titleHeaderRom);
    titleHeaderDevice.Reset();
    AssertTrue(titleHeaderDevice.UserHeader.IsValid, "MARS user header should allow game module names instead of only the literal MARS string");
    AssertEqual(0x0600_0400u, titleHeaderDevice.MasterSh2.PC);
    AssertEqual(0x0600_0402u, titleHeaderDevice.SlaveSh2.PC);
    AssertEqual((byte)0xE1, titleHeaderDevice.Sdram[0]);
    AssertEqual((byte)0x88, titleHeaderDevice.Sdram[1]);

    byte[] stackAliasRom = new byte[0x10000];
    WriteAscii(stackAliasRom, 0x3C0, "MARS STACK TEST");
    WriteLong(stackAliasRom, 0x3D4, 0x0000_5000);
    WriteLong(stackAliasRom, 0x3D8, 0x0000_0000);
    WriteLong(stackAliasRom, 0x3DC, 0x0000_0120);
    WriteLong(stackAliasRom, 0x3E0, 0x0600_0000);
    WriteLong(stackAliasRom, 0x3E4, 0x0600_0006);
    WriteLong(stackAliasRom, 0x3E8, 0x0600_0100);
    WriteLong(stackAliasRom, 0x3EC, 0x0600_0100);
    WriteWord(stackAliasRom, 0x5000, 0xD004); // MOV.L literal,R0
    WriteWord(stackAliasRom, 0x5002, 0x2F06); // MOV.L R0,@-R15
    WriteWord(stackAliasRom, 0x5004, 0x61F6); // MOV.L @R15+,R1
    WriteWord(stackAliasRom, 0x5006, 0x001B); // SLEEP
    WriteLong(stackAliasRom, 0x5014, 0x1234_5678);
    WriteLong(stackAliasRom, 0x5104, 0x0C04_0000);
    ThirtyTwoXDevice stackAliasDevice = new(stackAliasRom);
    stackAliasDevice.Reset();
    stackAliasDevice.MasterSh2.Run(8);
    AssertEqual(0x1234_5678u, stackAliasDevice.MasterSh2.R[1]);

}

void ThirtyTwoXAdapterControlAndCommunicationPorts()
{
    byte[] rom = new byte[0x100];
    WriteWord(rom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(rom, 0x02, 0x6101); // MOV.W @R0,R1
    WriteWord(rom, 0x04, 0x001B); // SLEEP
    WriteLong(rom, 0x14, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset));

    ThirtyTwoXDevice device = new(rom);
    device.Reset();
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset, 0x1234);
    AssertEqual(0, device.RunSh2(8));

    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    AssertTrue(!device.Sh2HeldInReset, "adapter control should release SH-2s");
    int executed = device.RunSh2(8);

    AssertEqual(6, executed);
    AssertEqual(0x0000_1234u, device.MasterSh2.R[1]);
    AssertEqual(0x0000_1234u, device.SlaveSh2.R[1]);
    AssertTrue(device.MasterSh2.Halted, "SH-2 should stop on SLEEP after reading communication port");

    byte[] cachedAliasRom = new byte[0x100];
    WriteWord(cachedAliasRom, 0x00, 0xD006); // MOV.L @(literal,PC),R0
    WriteWord(cachedAliasRom, 0x02, 0x6101); // MOV.W @R0,R1
    WriteWord(cachedAliasRom, 0x04, 0xD207); // MOV.L @(literal,PC),R2
    WriteWord(cachedAliasRom, 0x06, 0x2211); // MOV.W R1,@R2
    WriteWord(cachedAliasRom, 0x08, 0x001B); // SLEEP
    WriteLong(cachedAliasRom, 0x1C, ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart + ThirtyTwoXHardwareProfile.CommunicationPortOffset);
    WriteLong(cachedAliasRom, 0x24, ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart + ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2);
    ThirtyTwoXDevice cachedAliasDevice = new(cachedAliasRom);
    cachedAliasDevice.Reset();
    cachedAliasDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset, 0xBEEF);
    cachedAliasDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    cachedAliasDevice.RunSh2(12);
    AssertEqual(0xFFFF_BEEFu, cachedAliasDevice.MasterSh2.R[1]);
    AssertEqual((ushort)0xBEEF, cachedAliasDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));

    ThirtyTwoXDevice handshakeDevice = new();
    handshakeDevice.Reset();
    handshakeDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    AssertEqual((ushort)0x4D5F, handshakeDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0));
    AssertEqual((ushort)0x4F4B, handshakeDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    AssertEqual((ushort)0x535F, handshakeDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));
    AssertEqual((ushort)0x4F4B, handshakeDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6));
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        handshakeDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    AssertEqual((ushort)0x0000, handshakeDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset));
    AssertEqual((byte)0x00, handshakeDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset));

    ThirtyTwoXDevice twoPhaseHandshakeDevice = new();
    twoPhaseHandshakeDevice.Reset();
    twoPhaseHandshakeDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        twoPhaseHandshakeDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    AssertTrue(twoPhaseHandshakeDevice.BootRomHandshakePending, "clearing boot communication before reading the signature should keep the boot signature available");
    AssertEqual((ushort)0x4D5F, twoPhaseHandshakeDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0));
    AssertEqual((ushort)0x535F, twoPhaseHandshakeDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        twoPhaseHandshakeDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    AssertTrue(!twoPhaseHandshakeDevice.BootRomHandshakePending, "clearing the boot signature after it was observed should release the SH-2 program");
    AssertEqual((ushort)0x0000, twoPhaseHandshakeDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset));

    byte[] sh2ReadyRom = new byte[0x600];
    WriteWord(sh2ReadyRom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(sh2ReadyRom, 0x02, 0xE14D); // MOV #$4D,R1
    WriteWord(sh2ReadyRom, 0x04, 0x2010); // MOV.B R1,@R0
    WriteWord(sh2ReadyRom, 0x06, 0x001B); // SLEEP
    WriteLong(sh2ReadyRom, 0x14, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset));
    WriteAscii(sh2ReadyRom, 0x3C0, "MARS READY TEST");
    WriteLong(sh2ReadyRom, 0x3D4, 0x0000_0000);
    WriteLong(sh2ReadyRom, 0x3D8, 0x0000_0000);
    WriteLong(sh2ReadyRom, 0x3DC, 0x0000_0020);
    WriteLong(sh2ReadyRom, 0x3E0, 0x0600_0000);
    WriteLong(sh2ReadyRom, 0x3E4, 0x0600_0000);
    WriteLong(sh2ReadyRom, 0x3E8, 0x0600_0000);
    WriteLong(sh2ReadyRom, 0x3EC, 0x0600_0000);
    ThirtyTwoXDevice sh2ReadyDevice = new(sh2ReadyRom);
    sh2ReadyDevice.Reset();
    sh2ReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    AssertTrue(sh2ReadyDevice.BootRomHandshakePending, "boot signature should be pending immediately after SH-2 release");
    AssertEqual(0, sh2ReadyDevice.RunSh2(8));
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        sh2ReadyDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    AssertTrue(!sh2ReadyDevice.BootRomHandshakePending, "MARS user programs should run after the post-start boot signature is published");
    AssertEqual((ushort)0x4D5F, sh2ReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset));
    AssertEqual((ushort)0x4F4B, sh2ReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    AssertTrue(!sh2ReadyDevice.BootRomHandshakePending, "post-start boot signature should be readable without holding normal MARS user code");
    AssertEqual((ushort)0x535F, sh2ReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));
    AssertEqual((ushort)0x4F4B, sh2ReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6));
    AssertEqual((ushort)0x4F4B, sh2ReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    sh2ReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset, 0x0000);
    AssertEqual((ushort)0x0000, sh2ReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset));

    byte[] postStartClobberRom = (byte[])sh2ReadyRom.Clone();
    WriteWord(postStartClobberRom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(postStartClobberRom, 0x02, 0xE100); // MOV #0,R1
    WriteWord(postStartClobberRom, 0x04, 0x2011); // MOV.W R1,@R0
    WriteWord(postStartClobberRom, 0x06, 0x001B); // SLEEP
    ThirtyTwoXDevice postStartClobberDevice = new(postStartClobberRom);
    postStartClobberDevice.Reset();
    postStartClobberDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        postStartClobberDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    AssertTrue(postStartClobberDevice.RunSh2(8) > 0, "SH-2 code should run while post-start signature is pending");
    AssertEqual((ushort)0x4D5F, postStartClobberDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset));
    AssertEqual((ushort)0x4F4B, postStartClobberDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    AssertEqual((ushort)0x535F, postStartClobberDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));
    AssertEqual((ushort)0x4F4B, postStartClobberDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6));
    AssertEqual((ushort)0x0000, postStartClobberDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset));
    postStartClobberDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset, 0x0000);
    AssertEqual((ushort)0x0000, postStartClobberDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));

    ThirtyTwoXDevice postStartMailboxDevice = new(sh2ReadyRom);
    postStartMailboxDevice.Reset();
    postStartMailboxDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        postStartMailboxDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    WriteSh2WordForTest(
        postStartMailboxDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2),
        0x0080);
    AssertEqual((ushort)0x4D5F, postStartMailboxDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset));
    AssertEqual((ushort)0x4F4B, postStartMailboxDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    AssertEqual((ushort)0x535F, postStartMailboxDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));
    AssertEqual((ushort)0x4F4B, postStartMailboxDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6));
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        postStartMailboxDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    AssertEqual((ushort)0x0080, postStartMailboxDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));

    ThirtyTwoXDevice postStartSh2MailboxDevice = new(sh2ReadyRom);
    postStartSh2MailboxDevice.Reset();
    postStartSh2MailboxDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        postStartSh2MailboxDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    WriteSh2WordForTest(
        postStartSh2MailboxDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2),
        0xBEEF);
    AssertEqual((ushort)0x4F4B, postStartSh2MailboxDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    AssertEqual((ushort)0xBEEF, ReadSh2WordForTest(postStartSh2MailboxDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2)));

    ThirtyTwoXDevice postStartReadyDevice = new(sh2ReadyRom);
    postStartReadyDevice.Reset();
    postStartReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    AssertEqual((ushort)0x4D5F, postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0));
    AssertEqual((ushort)0x4F4B, postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    AssertEqual((ushort)0x535F, postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));
    AssertEqual((ushort)0x4F4B, postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6));
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        postStartReadyDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    WriteSh2WordForTest(
        postStartReadyDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0),
        0x4D52);
    WriteSh2WordForTest(
        postStartReadyDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2),
        0x4459);
    WriteSh2WordForTest(
        postStartReadyDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4),
        0x5352);
    WriteSh2WordForTest(
        postStartReadyDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6),
        0x4459);
    AssertEqual((ushort)0x4D52, postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0));
    AssertEqual((ushort)0x4459, postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    AssertEqual((ushort)0x5352, postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));
    AssertEqual((ushort)0x4459, postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6));
    AssertEqual((byte)0x4D, postStartReadyDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0));
    AssertEqual((byte)0x52, postStartReadyDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 1));
    postStartReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0, 0x0000);
    postStartReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2, 0x0000);
    postStartReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4, 0x0000);
    postStartReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6, 0x0000);
    AssertEqual((ushort)0x0000, ReadSh2WordForTest(postStartReadyDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0)));
    AssertEqual((ushort)0x0000, ReadSh2WordForTest(postStartReadyDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2)));
    AssertEqual((ushort)0x0000, ReadSh2WordForTest(postStartReadyDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4), cpuIndex: 1));
    AssertEqual((ushort)0x0000, ReadSh2WordForTest(postStartReadyDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6), cpuIndex: 1));
    _ = postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0);
    _ = postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4);
    AssertEqual((ushort)0x0000, postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0));
    AssertEqual((ushort)0x0000, postStartReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));

    AssertTrue(!sh2ReadyDevice.BootRomHandshakePending, "valid 32X user programs should run after the host observes the post-start boot signature");
    AssertTrue(sh2ReadyDevice.RunSh2(8) > 0, "SH-2 application code should be able to publish ready flags after the boot signature");
    AssertEqual((byte)0x4D, sh2ReadyDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset));

    byte[] launchRom = new byte[0x6000];
    WriteAscii(launchRom, 0x3C0, "STAR LAUNCH TEST");
    WriteLong(launchRom, 0x3D4, 0x0000_5000);
    WriteLong(launchRom, 0x3D8, 0x0000_0000);
    WriteLong(launchRom, 0x3DC, 0x0000_0500);
    WriteLong(launchRom, 0x3E0, 0x0600_0400);
    WriteLong(launchRom, 0x3E4, 0x0600_0402);
    WriteLong(launchRom, 0x3E8, 0x0600_0000);
    WriteLong(launchRom, 0x3EC, 0x0600_0200);
    WriteWord(launchRom, 0x5400, 0x001B); // SLEEP
    WriteWord(launchRom, 0x5402, 0x001B); // SLEEP
    ThirtyTwoXDevice launchDevice = new(launchRom);
    launchDevice.Reset();
    launchDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    launchDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4);
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        launchDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    AssertTrue(!launchDevice.BootRomHandshakePending, "STAR launch headers should expose the post-start boot ready signature without a handshake hold");
    AssertTrue(launchDevice.BootRomLaunchPending, "STAR launch headers should hold SH-2 user code until the 68000 sends a launch command");
    AssertEqual((ushort)0x4D5F, launchDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset));
    AssertEqual((ushort)0x535F, launchDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));
    AssertEqual((ushort)0x4D5F, launchDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset));
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        launchDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    AssertTrue(launchDevice.BootRomLaunchPending, "clearing the post-start signature should not release STAR launch headers");
    AssertEqual(0, launchDevice.RunSh2(4));
    launchDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset, 0x5348);
    AssertTrue(!launchDevice.BootRomLaunchPending, "host launch command should release STAR SH-2 user code");
    AssertEqual(2, launchDevice.RunSh2(4));
    AssertTrue(launchDevice.MasterSh2.Halted && launchDevice.SlaveSh2.Halted, "released STAR SH-2 programs should execute after launch command");

    byte[] launchChecksumRom = (byte[])launchRom.Clone();
    WriteWord(launchChecksumRom, 0x18E, 0x1234);
    ThirtyTwoXDevice launchChecksumDevice = new(launchChecksumRom);
    launchChecksumDevice.Reset();
    launchChecksumDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    launchChecksumDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4);
    for (ushort offset = 0; offset < 8; offset += 2)
    {
        launchChecksumDevice.WriteSystemRegisterWord((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + offset), 0x0000);
    }

    AssertTrue(launchChecksumDevice.BootRomLaunchPending, "STAR launch headers with checksums should still wait for the host launch command");
    launchChecksumDevice.WriteSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0, 0x53);
    launchChecksumDevice.WriteSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 1, 0x48);
    launchChecksumDevice.WriteSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2, 0x47);
    launchChecksumDevice.WriteSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 3, 0x4F);
    AssertTrue(!launchChecksumDevice.BootRomLaunchPending, "byte-wise SHGO should release STAR SH-2 user code even when the ROM has a checksum");
    AssertEqual(2, launchChecksumDevice.RunSh2(4));

    byte[] checksumRom = new byte[0x200];
    WriteWord(checksumRom, 0x18E, 0xBEEF);
    ThirtyTwoXDevice checksumDevice = new(checksumRom);
    checksumDevice.Reset();
    checksumDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    AssertEqual((ushort)0xBEEF, checksumDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8));

    byte[] staleChecksumRom = new byte[0x200];
    WriteWord(staleChecksumRom, 0x18E, 0xD746);
    ThirtyTwoXDevice staleChecksumDevice = new(staleChecksumRom);
    staleChecksumDevice.Reset();
    staleChecksumDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    WriteSh2WordForTest(
        staleChecksumDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8),
        0x534C,
        cpuIndex: 1);
    AssertEqual((ushort)0xD746, staleChecksumDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8));

    ThirtyTwoXDevice staleChecksumByteDevice = new(staleChecksumRom);
    staleChecksumByteDevice.Reset();
    staleChecksumByteDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    WriteSh2WordForTest(
        staleChecksumByteDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8),
        0x534C,
        cpuIndex: 1);
    AssertEqual((byte)0xD7, staleChecksumByteDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8));
    AssertEqual((byte)0x46, staleChecksumByteDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 9));

    ThirtyTwoXDevice publishedChecksumDevice = new(staleChecksumRom);
    publishedChecksumDevice.Reset();
    publishedChecksumDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    WriteSh2WordForTest(
        publishedChecksumDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8),
        0x534C,
        cpuIndex: 1);
    WriteSh2WordForTest(
        publishedChecksumDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8),
        0x0000,
        cpuIndex: 0);
    WriteSh2WordForTest(
        publishedChecksumDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12),
        0x000A,
        cpuIndex: 0);
    WriteSh2ByteForTest(
        publishedChecksumDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14),
        0x21);
    ThirtyTwoXDevice.ThirtyTwoXState publishedChecksumState = publishedChecksumDevice.CaptureState();
    publishedChecksumDevice.RestoreState(publishedChecksumState with
    {
        BootRomHandshakePending = false,
        BootRomSignatureReadbackActive = false,
        BootRomPostStartSignaturePending = true,
        BootRomChecksumPublished = true,
    });
    AssertEqual((ushort)0xD746, publishedChecksumDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8));

    ThirtyTwoXDevice sixtyEightUpDevice = new();
    sixtyEightUpDevice.Reset();
    sixtyEightUpDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12, 0x3638);
    sixtyEightUpDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14, 0x5550);
    sixtyEightUpDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12, 0x0000);
    sixtyEightUpDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14, 0x0000);
    AssertEqual((ushort)0x4F4B, sixtyEightUpDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14));

    ThirtyTwoXDevice sixtyEightUpGuardDevice = new();
    sixtyEightUpGuardDevice.Reset();
    sixtyEightUpGuardDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12, 0x0000);
    sixtyEightUpGuardDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14, 0x0000);
    AssertEqual((ushort)0x0000, sixtyEightUpGuardDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14));

    ThirtyTwoXDevice vdpControlReadyDevice = new();
    vdpControlReadyDevice.Reset();
    vdpControlReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    vdpControlReadyDevice.RestoreState(vdpControlReadyDevice.CaptureState() with
    {
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
        BootRomPostStartSignaturePending = false,
    });
    vdpControlReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12, 0x0002);
    vdpControlReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12, 0x0000);
    vdpControlReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2, 0x0000);
    vdpControlReadyDevice.NotifyM68kVdpControlLongWrite(0x4000, 0x0010);
    AssertEqual((ushort)0x4000, vdpControlReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12));
    AssertEqual((ushort)0x0010, vdpControlReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));

    ThirtyTwoXDevice vdpControlZeroLowDevice = new();
    vdpControlZeroLowDevice.Reset();
    vdpControlZeroLowDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    vdpControlZeroLowDevice.RestoreState(vdpControlZeroLowDevice.CaptureState() with
    {
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
        BootRomPostStartSignaturePending = false,
    });
    vdpControlZeroLowDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12, 0x0002);
    vdpControlZeroLowDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12, 0x0000);
    vdpControlZeroLowDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2, 0x0000);
    vdpControlZeroLowDevice.NotifyM68kVdpControlLongWrite(0x4000, 0x0000);
    AssertEqual((ushort)0x4000, vdpControlZeroLowDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12));
    AssertEqual((ushort)0x0001, vdpControlZeroLowDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    WriteSh2WordForTest(
        vdpControlZeroLowDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14),
        0x0001);
    AssertEqual((ushort)0x0001, vdpControlZeroLowDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12));
    WriteSh2WordForTest(
        vdpControlZeroLowDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14),
        0x0000);
    AssertEqual((ushort)0x0000, vdpControlZeroLowDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12));

    ThirtyTwoXDevice vdpControlWordReadyDevice = new();
    vdpControlWordReadyDevice.Reset();
    vdpControlWordReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    vdpControlWordReadyDevice.RestoreState(vdpControlWordReadyDevice.CaptureState() with
    {
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
        BootRomPostStartSignaturePending = false,
    });
    vdpControlWordReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12, 0x0002);
    vdpControlWordReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12, 0x0000);
    vdpControlWordReadyDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2, 0x0000);
    vdpControlWordReadyDevice.NotifyM68kVdpControlWrite(0x4000);
    vdpControlWordReadyDevice.NotifyM68kVdpControlWrite(0x0010);
    AssertEqual((ushort)0x4000, vdpControlWordReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12));
    AssertEqual((ushort)0x0010, vdpControlWordReadyDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));

    ThirtyTwoXDevice vdpControlGuardDevice = new();
    vdpControlGuardDevice.Reset();
    vdpControlGuardDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    ushort guardedLowWord = vdpControlGuardDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2);
    vdpControlGuardDevice.NotifyM68kVdpControlLongWrite(0x4000, 0x0010);
    AssertEqual((ushort)0x0000, vdpControlGuardDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12));
    AssertEqual(guardedLowWord, vdpControlGuardDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));

    ThirtyTwoXDevice vdpControlUnarmedDevice = new();
    vdpControlUnarmedDevice.Reset();
    vdpControlUnarmedDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    vdpControlUnarmedDevice.RestoreState(vdpControlUnarmedDevice.CaptureState() with
    {
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
        BootRomPostStartSignaturePending = false,
    });
    vdpControlUnarmedDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2, 0x0000);
    vdpControlUnarmedDevice.NotifyM68kVdpControlLongWrite(0x4000, 0x0010);
    AssertEqual((ushort)0x0000, vdpControlUnarmedDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12));
    AssertEqual((ushort)0x0000, vdpControlUnarmedDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));

    ThirtyTwoXDevice retailUpperCommandDevice = new();
    retailUpperCommandDevice.Reset();
    retailUpperCommandDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    retailUpperCommandDevice.RestoreState(retailUpperCommandDevice.CaptureState() with
    {
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
        BootRomPostStartSignaturePending = false,
    });
    retailUpperCommandDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12, 0x0011);
    WriteSh2WordForTest(
        retailUpperCommandDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12),
        0x0000);
    retailUpperCommandDevice.NotifyM68kVdpControlLongWrite(0x4000, 0x0082);
    AssertEqual((ushort)0x0000, retailUpperCommandDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 12));
    AssertEqual((ushort)0x0000, retailUpperCommandDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));

    byte[] maskRom = new byte[0x100];
    WriteWord(maskRom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(maskRom, 0x02, 0xE10F); // MOV #$0F,R1
    WriteWord(maskRom, 0x04, 0x2011); // MOV.W R1,@R0
    WriteWord(maskRom, 0x06, 0x001B); // SLEEP
    WriteLong(maskRom, 0x14, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.AdapterControlOffset));
    ThirtyTwoXDevice maskDevice = new(maskRom);
    maskDevice.Reset();
    maskDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    maskDevice.MasterSh2.Run(8);
    AssertTrue((maskDevice.MasterInterruptMask & 0x000F) == 0x000F, "SH-2 writes to $20004000 should update the SH-2 interrupt mask");
    AssertEqual((ushort)0x0083, maskDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset));

    byte[] commandInterruptRom = new byte[0x200];
    WriteWord(commandInterruptRom, 0x00, 0xE002); // MOV #2,R0
    WriteWord(commandInterruptRom, 0x02, 0x400E); // LDC R0,SR
    WriteWord(commandInterruptRom, 0x04, 0xAFFE); // BRA *
    WriteWord(commandInterruptRom, 0x06, 0x0009); // NOP
    WriteWord(commandInterruptRom, 0x80, 0xE155); // MOV #$55,R1
    WriteWord(commandInterruptRom, 0x82, 0x001B); // SLEEP
    WriteLong(commandInterruptRom, 0x40 + (68 * 4), ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x80);
    ThirtyTwoXDevice commandInterruptDevice = new(commandInterruptRom);
    commandInterruptDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    commandInterruptDevice.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x40);
    commandInterruptDevice.MasterSh2.Run(3);
    commandInterruptDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset, 0x0001);
    AssertEqual((ushort)0x0001, commandInterruptDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset));
    commandInterruptDevice.MasterSh2.RequestInterrupt(8, 68);
    commandInterruptDevice.MasterSh2.Run(8);
    AssertEqual(0x0000_0055u, commandInterruptDevice.MasterSh2.R[1]);
    AssertEqual((ushort)0x0001, commandInterruptDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset));

    byte[] commandClearRom = new byte[0x40];
    WriteWord(commandClearRom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(commandClearRom, 0x02, 0xE101); // MOV #1,R1
    WriteWord(commandClearRom, 0x04, 0x2011); // MOV.W R1,@R0
    WriteWord(commandClearRom, 0x06, 0x001B); // SLEEP
    WriteLong(commandClearRom, 0x14, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommandInterruptClearOffset));
    ThirtyTwoXDevice commandClearDevice = new(commandClearRom);
    commandClearDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    commandClearDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset, 0x0002);
    AssertEqual((ushort)0x0002, commandClearDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset));
    commandClearDevice.SlaveSh2.Run(4);
    AssertEqual((ushort)0x0000, commandClearDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset));

    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0081);
    AssertTrue(device.Sh2HeldInReset, "clearing RES with REN set should hold SH-2s in reset");
    AssertEqual(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart, device.MasterSh2.PC);
}

void ThirtyTwoXPackedPixelsUseFullPaletteIndex()
{
    ThirtyTwoXDevice device = new();
    device.Reset();
    device.WritePaletteWord(0x18E, 0x001F);
    device.WriteFrameBufferWord(0, 0x0100);
    device.WriteFrameBufferByte(0x200, 0xC7);
    device.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);

    byte[] frame = new byte[ThirtyTwoXHardwareProfile.NominalWidth * ThirtyTwoXHardwareProfile.NtscVisibleLines * 3];
    device.CompositeFrameRgbInto(frame);

    AssertEqual((byte)0xFF, frame[0]);
    AssertEqual((byte)0x00, frame[1]);
    AssertEqual((byte)0x00, frame[2]);
    AssertTrue(device.LastCompositeWrittenPixels > 0, "packed-pixel composite should use all eight palette index bits");
}

void ThirtyTwoXCachedFrameBufferBytesMapBeforeCartridgeRom()
{
    byte[] rom = new byte[0x400];
    rom[0x10] = 0x5A;
    ThirtyTwoXDevice device = new(rom);
    device.Reset();
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x8000);

    uint cachedFrameBufferAddress = ThirtyTwoXHardwareProfile.Sh2FrameBufferCachedStart + 0x10;
    WriteSh2ByteForTest(device, cachedFrameBufferAddress, 0xC7);

    AssertEqual((byte)0xC7, device.ReadFrameBufferByte(0x10));
    AssertEqual((byte)0xC7, ReadSh2ByteForTest(device, cachedFrameBufferAddress));
}

void ThirtyTwoXFixedCartridgeCacheTagsUseSh2Address()
{
    byte[] rom = new byte[0x100];
    rom[0x20] = 0x5A;
    ThirtyTwoXDevice device = new(rom);
    device.Reset();

    WriteSh2ByteForTest(device, 0xC000_0020, 0xAA);
    WriteSh2LongForTest(device, 0x6000_0024, 0x0000_0000);

    AssertEqual((byte)0x5A, ReadSh2ByteForTest(device, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedCachedStart + 0x20));
}

void ThirtyTwoXPackedPaletteZeroIsTransparent()
{
    ThirtyTwoXDevice device = new();
    device.Reset();
    device.WritePaletteWord(0, 0x001F);
    device.WritePaletteWord(2, 0x03E0);
    device.WriteFrameBufferWord(0, 0x0100);
    device.WriteFrameBufferByte(0x200, 0);
    device.WriteFrameBufferByte(0x201, 1);
    device.WriteVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset, 0x0001);

    byte[] frame = new byte[ThirtyTwoXHardwareProfile.NominalWidth * ThirtyTwoXHardwareProfile.NtscVisibleLines * 3];
    frame[0] = 9;
    frame[1] = 8;
    frame[2] = 7;
    device.CompositeFrameRgbInto(frame);

    AssertEqual((byte)9, frame[0]);
    AssertEqual((byte)8, frame[1]);
    AssertEqual((byte)7, frame[2]);
    AssertEqual((byte)0, frame[3]);
    AssertEqual((byte)255, frame[4]);
    AssertEqual((byte)0, frame[5]);
    AssertEqual(1, device.LastCompositeWrittenPixels);
}

void ThirtyTwoXCommunicationByteReadWriteEdge()
{
    byte[] rom = new byte[0x100];
    WriteWord(rom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(rom, 0x02, 0xE144); // MOV #$44,R1
    WriteWord(rom, 0x04, 0x2010); // MOV.B R1,@R0
    WriteWord(rom, 0x06, 0x001B); // SLEEP
    WriteLong(rom, 0x14, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));

    ThirtyTwoXDevice device = new(rom);
    device.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    device.MasterSh2.Run(4);

    AssertEqual((byte)0x44, device.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    AssertEqual((ushort)0x4400, device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));

    byte[] staleRom = new byte[0x100];
    WriteWord(staleRom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(staleRom, 0x02, 0xE144); // MOV #$44,R1
    WriteWord(staleRom, 0x04, 0x2010); // MOV.B R1,@R0
    WriteWord(staleRom, 0x06, 0x001B); // SLEEP
    WriteLong(staleRom, 0x14, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));

    ThirtyTwoXDevice staleDevice = new(staleRom);
    staleDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    staleDevice.MasterSh2.Run(4);

    AssertEqual((byte)0x00, staleDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));
    AssertEqual((byte)0x44, staleDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4));

    byte[] oddLaneRom = new byte[0x100];
    WriteWord(oddLaneRom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(oddLaneRom, 0x02, 0xE144); // MOV #$44,R1
    WriteWord(oddLaneRom, 0x04, 0x2010); // MOV.B R1,@R0
    WriteWord(oddLaneRom, 0x06, 0x001B); // SLEEP
    WriteLong(oddLaneRom, 0x14, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 3));

    ThirtyTwoXDevice oddLaneDevice = new(oddLaneRom);
    oddLaneDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    oddLaneDevice.MasterSh2.Run(4);

    AssertEqual((byte)0x44, oddLaneDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    oddLaneDevice.WriteSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2, 0x00);
    AssertEqual((ushort)0x0000, oddLaneDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    ThirtyTwoXDevice pairedAckDevice = new();
    pairedAckDevice.Reset();
    WriteSh2ByteForTest(pairedAckDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2), 0x04);
    WriteSh2ByteForTest(pairedAckDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 3), 0x04);
    AssertEqual((byte)0x04, pairedAckDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2));
    AssertEqual((byte)0x00, ReadSh2ByteForTest(pairedAckDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 3)));

    ThirtyTwoXDevice commandAckDevice = new();
    commandAckDevice.Reset();
    WriteSh2ByteForTest(commandAckDevice, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 1), 0x01);

    AssertEqual((byte)0x00, commandAckDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset));
    AssertEqual((byte)0x01, commandAckDevice.ReadSystemRegisterByte(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 1));

    ThirtyTwoXDevice highMailboxDevice = new();
    highMailboxDevice.Reset();
    WriteSh2WordForTest(
        highMailboxDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14),
        0x4F4B,
        cpuIndex: 1);
    AssertEqual((ushort)0x4F4B, ReadSh2WordForTest(
        highMailboxDevice,
        ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14)));
    AssertEqual((ushort)0x4F4B, highMailboxDevice.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 14));
}

void ThirtyTwoXM68kSystemHandshakesSyncSh2()
{
    byte[] rom = new byte[0x400];
    WriteAscii(rom, 0x100, "SEGA 32X");
    WriteWord(rom, 0x00, 0xD004); // MOV.L @(literal,PC),R0
    WriteWord(rom, 0x02, 0xE15A); // MOV #$5A,R1
    WriteWord(rom, 0x04, 0x2011); // MOV.W R1,@R0
    WriteWord(rom, 0x06, 0x001B); // SLEEP
    WriteLong(rom, 0x14, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset));

    GenesisBus bus = new(CartridgeImage.FromBytes(rom), new Vdp(), new Psg(), new Ym2612());
    AssertTrue(bus.ThirtyTwoX is not null, "32X test cartridge should attach the 32X device");
    ThirtyTwoXDevice device = bus.ThirtyTwoX!;
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6, 0x0001);
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6, 0x0000);
    device.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    bus.CurrentMasterCycle = 128;

    AssertEqual((ushort)0x0000, bus.ReadWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset)));
    AssertEqual((ushort)0x005A, bus.ReadWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.CommunicationPortOffset)));
    AssertTrue(device.MasterSh2.Halted, "68000 communication read should give the SH-2 enough time to publish its handshake word");

    byte[] irqRom = new byte[0x400];
    WriteAscii(irqRom, 0x100, "SEGA 32X");
    WriteWord(irqRom, 0x00, 0x001B); // SLEEP
    WriteLong(irqRom, 68 * 4, ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x100);
    WriteLong(irqRom, 0x120, ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.CommandInterruptClearOffset));
    WriteWord(irqRom, 0x100, 0xD107); // MOV.L @(literal,PC),R1
    WriteWord(irqRom, 0x102, 0xE201); // MOV #1,R2
    WriteWord(irqRom, 0x104, 0x2121); // MOV.W R2,@R1
    WriteWord(irqRom, 0x106, 0x001B); // SLEEP
    WriteAscii(irqRom, 0x180, "32X");
    GenesisBus irqBus = new(CartridgeImage.FromBytes(irqRom), new Vdp(), new Psg(), new Ym2612());
    AssertTrue(irqBus.ThirtyTwoX is not null, "32X interrupt test cartridge should attach the 32X device");
    ThirtyTwoXDevice irqDevice = irqBus.ThirtyTwoX!;
    irqDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6, 0x0001);
    irqDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    irqDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6, 0x0000);
    irqDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    irqDevice.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    irqBus.CurrentMasterCycle = 256;
    irqBus.WriteByte(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.InterruptControlOffset + 1), 0x01);
    AssertEqual((ushort)0x0001, irqBus.ReadWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.InterruptControlOffset)));
}

void ThirtyTwoXSh2WatchdogKeyedWrites()
{
    static byte ReadSh2ByteForTest(ThirtyTwoXDevice target, uint address)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Byte helper was not found");
        return (byte)method.Invoke(target, [address, 0])!;
    }

    static void WriteSh2WordForTest(ThirtyTwoXDevice target, uint address, ushort value)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Word", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Word helper was not found");
        method.Invoke(target, [address, value, 0]);
    }

    ThirtyTwoXDevice device = new();
    device.Reset();
    AssertEqual((byte)0x00, ReadSh2ByteForTest(device, 0xFFFF_FE80));
    AssertEqual((byte)0x00, ReadSh2ByteForTest(device, 0xFFFF_FE81));
    AssertEqual((byte)0x1F, ReadSh2ByteForTest(device, 0xFFFF_FE83));

    WriteSh2WordForTest(device, 0xFFFF_FE80, 0x5AFF);
    AssertEqual((byte)0x00, ReadSh2ByteForTest(device, 0xFFFF_FE80));
    AssertEqual((byte)0xFF, ReadSh2ByteForTest(device, 0xFFFF_FE81));

    WriteSh2WordForTest(device, 0xFFFF_FE80, 0xA538);
    AssertEqual((byte)0x20, ReadSh2ByteForTest(device, 0xFFFF_FE80));

    WriteSh2WordForTest(device, 0xFFFF_FE80, 0x1234);
    AssertEqual((byte)0x20, ReadSh2ByteForTest(device, 0xFFFF_FE80));
    AssertEqual((byte)0xFF, ReadSh2ByteForTest(device, 0xFFFF_FE81));
}

void ThirtyTwoXSh2WatchdogIntervalInterrupt()
{
    static byte ReadSh2ByteForTest(ThirtyTwoXDevice target, uint address)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Byte helper was not found");
        return (byte)method.Invoke(target, [address, 0])!;
    }

    static void WriteSh2ByteForTest(ThirtyTwoXDevice target, uint address, byte value)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Byte helper was not found");
        method.Invoke(target, [address, value, 0]);
    }

    static void WriteSh2WordForTest(ThirtyTwoXDevice target, uint address, ushort value)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Word", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Word helper was not found");
        method.Invoke(target, [address, value, 0]);
    }

    static void InvokePrivate(ThirtyTwoXDevice target, string name, params object[] args)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{name} helper was not found");
        method.Invoke(target, args);
    }

    ThirtyTwoXDevice device = new();
    device.Reset();
    WriteSh2ByteForTest(device, 0xFFFF_FEE3, 0xF0);
    WriteSh2ByteForTest(device, 0xFFFF_FEE4, 0x40);
    WriteSh2WordForTest(device, 0xFFFF_FE80, 0x5AFF);
    WriteSh2WordForTest(device, 0xFFFF_FE80, 0xA538);

    InvokePrivate(device, "AdvanceSh2Watchdog", 0, 2);
    InvokePrivate(device, "RequestPendingInterrupts");

    AssertEqual((byte)0xA0, ReadSh2ByteForTest(device, 0xFFFF_FE80));
    AssertEqual((byte)0x00, ReadSh2ByteForTest(device, 0xFFFF_FE81));
    AssertEqual(15, device.MasterSh2.PendingInterruptLevel);
    AssertEqual(0x40, device.MasterSh2.PendingInterruptVectorNumber);
}

void ThirtyTwoXSh2DivisionUnit()
{
    static uint ReadSh2LongForTest(ThirtyTwoXDevice target, uint address)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Long", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Long helper was not found");
        return (uint)(method.Invoke(target, [address, 0]) ?? 0u);
    }

    static ushort ReadSh2WordForTest(ThirtyTwoXDevice target, uint address)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Word", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Word helper was not found");
        return (ushort)(method.Invoke(target, [address, 0]) ?? (ushort)0);
    }

    static void WriteSh2LongForTest(ThirtyTwoXDevice target, uint address, uint value)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Long", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Long helper was not found");
        method.Invoke(target, [address, value, 0]);
    }

    static void WriteSh2WordForTest(ThirtyTwoXDevice target, uint address, ushort value)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Word", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WriteSh2Word helper was not found");
        method.Invoke(target, [address, value, 0]);
    }

    ThirtyTwoXDevice device = new(new byte[0x100]);
    device.Reset();
    WriteSh2LongForTest(device, 0xFFFF_FF00, 7);
    WriteSh2LongForTest(device, 0xFFFF_FF04, 100);
    AssertEqual(14u, ReadSh2LongForTest(device, 0xFFFF_FF04));
    AssertEqual(2u, ReadSh2LongForTest(device, 0xFFFF_FF10));
    AssertEqual(0u, ReadSh2LongForTest(device, 0xFFFF_FF08));

    WriteSh2LongForTest(device, 0xFFFF_FF00, unchecked((uint)-8));
    WriteSh2LongForTest(device, 0xFFFF_FF04, 100);
    AssertEqual(unchecked((uint)-12), ReadSh2LongForTest(device, 0xFFFF_FF04));
    AssertEqual(4u, ReadSh2LongForTest(device, 0xFFFF_FF10));

    WriteSh2LongForTest(device, 0xFFFF_FF00, 9);
    WriteSh2LongForTest(device, 0xFFFF_FF10, 0);
    WriteSh2LongForTest(device, 0xFFFF_FF14, 0x0000_0100);
    AssertEqual(28u, ReadSh2LongForTest(device, 0xFFFF_FF14));
    AssertEqual(4u, ReadSh2LongForTest(device, 0xFFFF_FF10));
    AssertEqual(28u, ReadSh2LongForTest(device, 0xFFFF_FF1C));
    AssertEqual(4u, ReadSh2LongForTest(device, 0xFFFF_FF18));

    WriteSh2WordForTest(device, 0xFFFF_FF08, 0);
    WriteSh2LongForTest(device, 0xFFFF_FF00, 0);
    WriteSh2LongForTest(device, 0xFFFF_FF04, 123);
    AssertEqual(0x7FFF_FFFFu, ReadSh2LongForTest(device, 0xFFFF_FF04));
    AssertEqual((ushort)1, ReadSh2WordForTest(device, 0xFFFF_FF08));
}

void ThirtyTwoXPwmInterruptsAdvanceWithExecutedSh2Cycles()
{
    byte[] rom = new byte[0x300];
    WriteWord(rom, 0x00, 0xE002); // MOV #2,R0
    WriteWord(rom, 0x02, 0x400E); // LDC R0,SR
    WriteWord(rom, 0x04, 0xAFFE); // BRA *
    WriteWord(rom, 0x06, 0x0009); // NOP
    WriteWord(rom, 0x80, 0xE15A); // MOV #$5A,R1
    WriteWord(rom, 0x82, 0x001B); // SLEEP
    WriteLong(rom, 0x40 + (67 * 4), ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x80);

    ThirtyTwoXDevice device = new(rom);
    device.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    device.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x40);
    device.MasterSh2.Run(3);
    device.RestoreState(device.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        VdpAccessGrantedToSh2 = true,
        MasterInterruptMask = 0x0001,
    });

    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmControlOffset, 0x0105);
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmCycleOffset, 0x0100);
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, 0x0200);

    device.RunSh2Cycles(4);
    AssertEqual(0, device.MasterSh2.PendingInterruptLevel);
    AssertTrue(!device.CaptureState().MasterPwmInterruptPending, "short SH-2 run should not pre-fire a whole budget worth of PWM time");

    device.RunSh2Cycles(512);
    AssertEqual(0x0000_005Au, device.MasterSh2.R[1]);

    ThirtyTwoXDevice tmZeroDevice = new(rom);
    tmZeroDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    tmZeroDevice.MasterSh2.SetVbr(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart + 0x40);
    tmZeroDevice.MasterSh2.Run(3);
    tmZeroDevice.RestoreState(tmZeroDevice.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        VdpAccessGrantedToSh2 = true,
        MasterInterruptMask = 0x0001,
    });

    tmZeroDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmControlOffset, 0x0005);
    tmZeroDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmCycleOffset, 0x0010);
    tmZeroDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, 0x0200);
    tmZeroDevice.RunSh2Cycles(64);
    AssertTrue(!tmZeroDevice.CaptureState().MasterPwmInterruptPending, "PWM TM=0 should not behave like a one-cycle interval");
    tmZeroDevice.RunSh2Cycles(512);
    AssertTrue(tmZeroDevice.CaptureState().MasterPwmInterruptPending, "PWM TM=0 should decode as the longest 4-bit timer interval");

    ThirtyTwoXDevice retunedDevice = new(rom);
    retunedDevice.ResetSh2(ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
    retunedDevice.RestoreState(retunedDevice.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        VdpAccessGrantedToSh2 = true,
        MasterInterruptMask = 0x0001,
        PwmCycleCounter = 1,
        PwmTimerCounter = 1,
    });
    retunedDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmControlOffset, 0x0005);
    retunedDevice.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmCycleOffset, 0x0100);
    retunedDevice.RunSh2Cycles(4);
    AssertTrue(!retunedDevice.CaptureState().MasterPwmInterruptPending, "rewriting PWM timing registers should reset stale near-expired counters");
}

void ThirtyTwoXM68kBusShell()
{
    byte[] rom = new byte[0x400000];
    WriteAscii(rom, 0x100, "SEGA 32X");
    rom[0x1234] = 0x5A;
    rom[0x1235] = 0xC3;
    rom[0x000010] = 0x11;
    rom[0x000011] = 0x22;
    rom[0x000012] = 0x23;
    rom[0x000013] = 0x24;
    rom[0x100010] = 0x33;
    rom[0x100011] = 0x44;
    rom[0x100012] = 0x45;
    rom[0x100013] = 0x46;
    rom[0x200010] = 0x55;
    rom[0x200011] = 0x66;
    rom[0x200012] = 0x67;
    rom[0x200013] = 0x68;
    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();

    AssertTrue(machine.Bus.ThirtyTwoX is not null, "32X cartridge should attach the 32X shell");
    AssertEqual((uint)0x4D415253, machine.Bus.ReadLong(0xA1_30EC));

    machine.Bus.WriteWord(0xA1_5134, 0x0AAA);
    ThirtyTwoXDevice.PwmSnapshot pwm = machine.Bus.ThirtyTwoX!.CapturePwm();
    AssertEqual(1, pwm.Left.Length);
    AssertEqual((ushort)0x0AAA, pwm.Left[0]);
    AssertEqual((ushort)0x0AAA, machine.Bus.ReadWord(0xA1_5134));

    machine.Bus.WriteWord(0xA1_5200, 0x7ACE);
    AssertEqual((ushort)0x7ACE, machine.Bus.ReadWord(0xA1_5200));
    AssertTrue((machine.Bus.ThirtyTwoX.MasterInterruptMask & 0x8000) != 0, "68000-side 32X palette access should hand VDP access back to the SH-2 side");

    machine.Bus.WriteWord(0x84_0000, 0xBEEF);
    AssertEqual((byte)0xBE, machine.Bus.ThirtyTwoX.DrawFrameBuffer[0]);
    AssertEqual((byte)0xEF, machine.Bus.ThirtyTwoX.DrawFrameBuffer[1]);
    machine.Bus.WriteWord(0x86_0000, 0x00AA);
    AssertEqual((byte)0xBE, machine.Bus.ThirtyTwoX.DrawFrameBuffer[0]);
    AssertEqual((byte)0xAA, machine.Bus.ThirtyTwoX.DrawFrameBuffer[1]);
    machine.Bus.WriteWord(0xA1_5100, 0x0083);
    machine.Bus.WriteWord(0xA1_518A, 0x0001);
    machine.Bus.ThirtyTwoX.StepScanline(224, pal: false);
    AssertEqual(0, machine.Bus.ThirtyTwoX.DrawFrameBufferIndex);
    AssertEqual((ushort)0x0000, machine.Bus.ReadWord(0x84_0000));

    AssertEqual((ushort)0x5AC3, machine.Bus.ReadWord(0x88_1234));
    AssertEqual((ushort)0x1122, machine.Bus.ReadWord(0x90_0010));
    machine.Bus.WriteWord(0xA1_5104, 0x0001);
    AssertEqual((byte)0x33, machine.Bus.ReadByte(0x90_0010));
    AssertEqual((byte)0x44, machine.Bus.ReadByte(0x90_0011));
    AssertEqual((ushort)0x3344, machine.Bus.ReadWord(0x90_0010));
    AssertEqual(0x3344_4546u, machine.Bus.ReadLong(0x90_0010));
    AssertEqual((ushort)0x1122, machine.Bus.ReadWord(0x88_0010));
    machine.Bus.WriteByte(0xA1_5105, 0x02);
    AssertEqual((ushort)0x5566, machine.Bus.ReadWord(0x90_0010));
    AssertEqual(0x5566_6768u, machine.Bus.ReadLong(0x90_0010));

    MegaDrive.MegaDriveState state = machine.CaptureState();
    machine.Bus.WriteWord(0xA1_5200, 0x0000);
    machine.Bus.WriteWord(0xA1_5104, 0x0000);
    machine.RestoreState(state);
    AssertEqual((ushort)0x7ACE, machine.Bus.ReadWord(0xA1_5200));
    AssertEqual((ushort)0x0000, machine.Bus.ReadWord(0x84_0000));
    AssertEqual((ushort)0x5566, machine.Bus.ReadWord(0x90_0010));
}

void ThirtyTwoXM68kVectorRomMapping()
{
    byte[] rom = new byte[0x400000];
    WriteAscii(rom, 0x100, "SEGA 32X");
    WriteLong(rom, 0x78, 0x0000_09D0);
    WriteLong(rom, 0xC0, 0x08F9_0000);
    WriteLong(rom, 0x02B4, 0xDEAD_BEEF);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();

    AssertEqual(0x0000_09D0u, machine.Bus.ReadLong(0x0000_0078));
    AssertEqual(0x0000_0000u, machine.Bus.ReadLong(0x0000_0000));
    AssertEqual(0x08F9_0000u, machine.Bus.ReadLong(0x0000_00C0));
    machine.Bus.WriteWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.AdapterControlOffset), 0x0083);
    AssertEqual(0x0088_02B4u, machine.Bus.ReadLong(0x0000_0078));
    AssertEqual((ushort)0x0088, machine.Bus.ReadWord(0x0000_0078));
    AssertEqual((ushort)0x02B4, machine.Bus.ReadWord(0x0000_007A));
    AssertEqual((byte)0xB4, machine.Bus.ReadByte(0x0000_007B));
    AssertEqual(0x0000_0000u, machine.Bus.ReadLong(0x0000_0000));
    AssertEqual(0x08F9_0000u, machine.Bus.ReadLong(0x0000_00C0));
    AssertEqual((ushort)0xDEAD, machine.Bus.ReadWord(0x0088_02B4));
    AssertEqual((ushort)0xFFFF, machine.Bus.ReadWord(0x0000_0200));
    machine.Bus.WriteWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset), 0x0001);
    AssertEqual((ushort)0xDEAD, machine.Bus.ReadWord(0x0000_02B4));
}

void ThirtyTwoXM68kVBlankUsesGenesisLevel6()
{
    byte[] rom = new byte[0x400000];
    WriteAscii(rom, 0x100, "SEGA 32X");
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0088_0200);
    WriteLong(rom, 0x074, 0x0000_0310); // Native level 5 vector before ADEN.
    WriteLong(rom, 0x078, 0x0000_0320); // Native level 6 vector before ADEN.

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x46FC); // MOVE #$2300,SR
    EmitWord(rom, ref pc, 0x2300);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2300
    EmitWord(rom, ref pc, 0x2300);
    EmitWord(rom, ref pc, 0x60FE); // BRA *

    pc = 0x02AE; // Vector 29 custom ROM target: $008802AE.
    EmitWord(rom, ref pc, 0x4EF9); // JMP $00880310
    EmitLong(rom, ref pc, 0x0088_0310);
    pc = 0x02B4; // Vector 30 custom ROM target: $008802B4.
    EmitWord(rom, ref pc, 0x4EF9); // JMP $00880320
    EmitLong(rom, ref pc, 0x0088_0320);

    pc = 0x310;
    EmitWord(rom, ref pc, 0x7A05); // MOVEQ #5,D5
    EmitWord(rom, ref pc, 0x4E73); // RTE

    pc = 0x320;
    EmitWord(rom, ref pc, 0x7C06); // MOVEQ #6,D6
    EmitWord(rom, ref pc, 0x4E73); // RTE

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.Bus.WriteWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.AdapterControlOffset), 0x0083);
    machine.Vdp.WriteControlPort(0x8120); // Enable VBlank interrupt.

    for (int i = 0; i < 2; i++)
    {
        machine.StepInstruction();
    }

    AssertTrue(machine.MainCpu.Stopped, "CPU should stop before 32X VBlank");
    machine.RunFrame(20_000);
    AssertEqual(0u, machine.MainCpu.D[5]);
    AssertEqual(6u, machine.MainCpu.D[6]);
}

void ThirtyTwoXSh2BootRomReadyMarker()
{
    ThirtyTwoXDevice device = new();
    device.RestoreState(device.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
    });

    static byte ReadSh2ByteForTest(ThirtyTwoXDevice target, uint address)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Byte reflection hook missing");
        return (byte)method.Invoke(target, [address, 0])!;
    }

    static ushort ReadSh2WordForTest(ThirtyTwoXDevice target, uint address)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Word", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Word reflection hook missing");
        return (ushort)method.Invoke(target, [address, 0])!;
    }

    AssertEqual((byte)0x80, ReadSh2ByteForTest(device, 0x0000_0000));
    AssertEqual((byte)0x80, ReadSh2ByteForTest(device, 0x2000_0000));
    AssertEqual((byte)0x00, ReadSh2ByteForTest(device, 0x0000_0001));
    AssertEqual((byte)0x00, ReadSh2ByteForTest(device, 0x0000_0FFF));
    AssertEqual((ushort)0x000B, ReadSh2WordForTest(device, 0x0000_0000));
    AssertEqual((ushort)0x0009, ReadSh2WordForTest(device, 0x0000_0002));
}

void ThirtyTwoXSh2BootRomMapsOptionalBiosImages()
{
    byte[] masterBios = new byte[0x1000];
    byte[] slaveBios = new byte[0x1000];
    masterBios[0] = 0x12;
    masterBios[1] = 0x34;
    masterBios[2] = 0x56;
    slaveBios[0] = 0xAB;
    slaveBios[1] = 0xCD;
    slaveBios[2] = 0xEF;

    ThirtyTwoXDevice device = new(masterSh2Bios: masterBios, slaveSh2Bios: slaveBios);
    device.RestoreState(device.CaptureState() with
    {
        AdapterEnabled = true,
        Sh2ResetEnabled = true,
        Sh2ResetReleased = true,
        BootRomHandshakePending = false,
        BootRomLaunchPending = false,
    });

    static byte ReadSh2ByteForTest(ThirtyTwoXDevice target, uint address, int cpuIndex)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Byte reflection hook missing");
        return (byte)method.Invoke(target, [address, cpuIndex])!;
    }

    static ushort ReadSh2WordForTest(ThirtyTwoXDevice target, uint address, int cpuIndex)
    {
        System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Word", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReadSh2Word reflection hook missing");
        return (ushort)method.Invoke(target, [address, cpuIndex])!;
    }

    AssertEqual((byte)0x12, ReadSh2ByteForTest(device, 0x0000_0000, 0));
    AssertEqual((byte)0xAB, ReadSh2ByteForTest(device, 0x0000_0000, 1));
    AssertEqual((byte)0x56, ReadSh2ByteForTest(device, 0x2000_0002, 0));
    AssertEqual((byte)0xEF, ReadSh2ByteForTest(device, 0x2000_0002, 1));
    AssertEqual((ushort)0x1234, ReadSh2WordForTest(device, 0x0000_0000, 0));
    AssertEqual((ushort)0xABCD, ReadSh2WordForTest(device, 0x0000_0000, 1));
}

void ThirtyTwoXSh2RealBiosBootUsesResetVectors()
{
    byte[] masterBios = new byte[0x1000];
    byte[] slaveBios = new byte[0x1000];
    WriteLong(masterBios, 0x00, 0x0000_0140);
    WriteLong(masterBios, 0x04, 0x0604_0000);
    WriteLong(slaveBios, 0x00, 0x0000_0180);
    WriteLong(slaveBios, 0x04, 0x0603_F800);

    ThirtyTwoXDevice device = new(masterSh2Bios: masterBios, slaveSh2Bios: slaveBios, useRealSh2BiosBoot: true);
    device.Reset();

    AssertEqual(0x0000_0140u, device.MasterSh2.PC);
    AssertEqual(0x0604_0000u, device.MasterSh2.R[15]);
    AssertEqual(0x0000_0180u, device.SlaveSh2.PC);
    AssertEqual(0x0603_F800u, device.SlaveSh2.R[15]);

    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    AssertEqual(0x0000_0140u, device.MasterSh2.PC);
    AssertEqual(0x0604_0000u, device.MasterSh2.R[15]);
    AssertEqual(0x0000_0180u, device.SlaveSh2.PC);
    AssertEqual(0x0603_F800u, device.SlaveSh2.R[15]);
    AssertTrue(!device.BootRomHandshakePending, "real SH-2 BIOS boot should not seed the synthetic M_OK/S_OK handshake");
}

void CpuResetAndSimpleInstructions()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteWord(rom, 0x200, 0x7001); // MOVEQ #1,D0
    WriteWord(rom, 0x202, 0x7200); // MOVEQ #0,D1
    WriteWord(rom, 0x204, 0x4E71); // NOP
    WriteWord(rom, 0x206, 0x60FC); // BRA -4

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    AssertEqual(0x00FF_0000u, machine.MainCpu.A[7]);
    AssertEqual(0x0000_0200u, machine.MainCpu.PC);

    machine.StepInstruction();
    machine.StepInstruction();
    machine.StepInstruction();
    machine.StepInstruction();

    AssertEqual(1u, machine.MainCpu.D[0]);
    AssertEqual(0u, machine.MainCpu.D[1]);
    AssertEqual(0x0000_0204u, machine.MainCpu.PC);
}

void CodemastersDnldResetVectors()
{
    byte[] rom = CreateRom();
    WriteAscii(rom, 0x000, "DNLD");
    WriteLong(rom, 0x004, 0);
    WriteLong(rom, 0x008, 0x0001_FACC);
    WriteLong(rom, 0x00C, 0x0000_0200);
    WriteWord(rom, 0x200, 0x7007); // MOVEQ #7,D0

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    AssertEqual(0x0001_FACCu, machine.MainCpu.A[7]);

    machine.StepInstruction();
    AssertEqual(7u, machine.MainCpu.D[0]);
}

void VdpRegisterAndVramWrites()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8F04);
    AssertEqual((byte)4, vdp.AutoIncrement);

    vdp.WriteControlPort(0x4000);
    vdp.WriteControlPort(0x0000);
    vdp.WriteDataPort(0x1234);
    vdp.WriteDataPort(0xABCD);

    AssertEqual((byte)0x12, vdp.Vram[0]);
    AssertEqual((byte)0x34, vdp.Vram[1]);
    AssertEqual((byte)0xAB, vdp.Vram[4]);
    AssertEqual((byte)0xCD, vdp.Vram[5]);
}

void VdpByteWritesMirrorOntoDataBus()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Bus.WriteWord(0x00C0_0004, 0x8F02);
    machine.Bus.WriteWord(0x00C0_0004, 0x4000);
    machine.Bus.WriteWord(0x00C0_0004, 0x0000);
    machine.Bus.WriteByte(0x00C0_0000, 0x5A);

    AssertEqual((byte)0x5A, machine.Vdp.Vram[0]);
    AssertEqual((byte)0x5A, machine.Vdp.Vram[1]);
    AssertEqual((byte)0x02, machine.Vdp.AutoIncrement);
}

void VdpCommandDecodeAndHistory()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8200);
    vdp.WriteControlPort(0x8238);
    AssertEqual(2, vdp.RegisterWrites.Count);
    AssertEqual(2, vdp.RegisterWrites[0].Register);
    AssertEqual((byte)0x00, vdp.RegisterWrites[0].PreviousValue);
    AssertEqual((byte)0x38, vdp.RegisterWrites[1].Value);

    vdp.WriteControlPort(0x6800); // VRAM write at $A800.
    vdp.WriteControlPort(0x0002);
    vdp.WriteDataPort(0xBEEF);
    AssertEqual((byte)0xBE, vdp.Vram[0xA800]);
    AssertEqual((byte)0xEF, vdp.Vram[0xA801]);
    AssertEqual(1, vdp.ControlCommands.Count);
    AssertEqual((byte)0x01, vdp.ControlCommands[^1].Code);
    AssertEqual(0xA800u, vdp.ControlCommands[^1].Address);

    vdp.WriteControlPort(0xC004); // CRAM write at color 2.
    vdp.WriteControlPort(0x0000);
    vdp.WriteDataPort(0x0EEE);
    AssertEqual((ushort)0x0EEE, vdp.Cram[2]);
    AssertEqual((byte)0x03, vdp.ControlCommands[^1].Code);
    AssertEqual(0x0004u, vdp.ControlCommands[^1].Address);

    vdp.WriteControlPort(0x4006); // VSRAM write at entry 3.
    vdp.WriteControlPort(0x0010);
    vdp.WriteDataPort(0x0123);
    AssertEqual((ushort)0x0123, vdp.Vsram[3]);
    AssertEqual((byte)0x05, vdp.ControlCommands[^1].Code);
    AssertEqual(0x0006u, vdp.ControlCommands[^1].Address);

    Vdp.VdpDiagnostics diagnostics = vdp.GetDiagnostics();
    AssertEqual(2, diagnostics.PlaneARegisterWriteCount);
    AssertEqual((byte)0x05, diagnostics.LastCommandCode);
    AssertEqual(0x0006u, diagnostics.LastCommandAddress);
}

void VdpHvCounterAndInterruptStatus()
{
    Vdp vdp = new();
    vdp.BeginFrame(pal: false);

    AssertEqual(0x0084, vdp.ReadHvCounter());
    AssertTrue((vdp.Status & 0x0200) != 0, "FIFO should begin empty");

    Vdp.Interrupts line0 = vdp.StepScanline(0, pal: false);
    AssertTrue((line0 & Vdp.Interrupts.Horizontal) != 0, "line zero should trigger initial H interrupt when counter is zero");
    AssertTrue((vdp.Status & 0x0004) == 0, "HBlank status should not be set until the HBlank slice");

    vdp.SetHBlank(true);
    ushort status = vdp.ReadControlPort();
    AssertTrue((status & 0x0004) != 0, "control read should return current HBlank status");
    AssertTrue((vdp.Status & 0x0004) != 0, "control read should not clear current HBlank status");
    vdp.SetHBlank(false);
    AssertTrue((vdp.Status & 0x0004) == 0, "leaving HBlank should clear HBlank status");

    Vdp.Interrupts vblank = vdp.StepScanline(224, pal: false);
    AssertTrue((vblank & Vdp.Interrupts.Vertical) != 0, "line 224 should trigger V interrupt");
    AssertTrue((vdp.Status & 0x0008) != 0, "VBlank flag should be set");
    AssertTrue((vdp.Status & 0x0080) != 0, "V interrupt pending bit should be set");
    status = vdp.ReadControlPort();
    AssertTrue((status & 0x0008) != 0, "control read should return current VBlank status");
    AssertTrue((vdp.Status & 0x0008) != 0, "control read should not clear current VBlank status");
    AssertTrue((vdp.Status & 0x0080) != 0, "control read during VBlank should keep the V interrupt status visible for polling code");
    AssertEqual(0xDA84, vdp.ReadHvCounter());
}

void VdpVInterruptPendingSurvivesFrameBoundaryUntilStatusRead()
{
    Vdp vdp = new();
    vdp.BeginFrame(pal: false);

    _ = vdp.StepScanline(224, pal: false);
    AssertTrue((vdp.Status & 0x0080) != 0, "V interrupt pending bit should set at VBlank");

    _ = vdp.StepScanline(Vdp.NtscScanlines - 1, pal: false);
    AssertTrue((vdp.Status & 0x0008) == 0, "current VBlank status should clear at the frame boundary");
    AssertTrue((vdp.Status & 0x0080) != 0, "V interrupt pending bit should survive until the status port is read");

    vdp.BeginFrame(pal: false);
    AssertTrue((vdp.Status & 0x0080) != 0, "new frame setup should not discard an unread V interrupt pending bit");

    ushort status = vdp.ReadControlPort();
    AssertTrue((status & 0x0080) != 0, "status read should expose the pending V interrupt bit");
    AssertTrue((vdp.Status & 0x0080) == 0, "status read should clear the pending V interrupt bit");
}

void VdpM68kInterruptAcknowledgeClearsPendingFlags()
{
    Vdp vdp = new();
    vdp.BeginFrame(pal: false);

    _ = vdp.StepScanline(0, pal: false);
    AssertTrue(vdp.HInterruptPending, "line zero should set the H interrupt pending flag when the counter is zero");
    vdp.AcknowledgeM68kInterrupt(4);
    AssertTrue(!vdp.HInterruptPending, "level 4 acknowledge should clear the H interrupt pending flag");

    _ = vdp.StepScanline(224, pal: false);
    AssertTrue(vdp.VInterruptPending, "VBlank should set the V interrupt pending flag");
    vdp.AcknowledgeM68kInterrupt(6);
    AssertTrue(vdp.VInterruptPending, "level 6 acknowledge should leave the status-port V interrupt latch visible");
    AssertTrue((vdp.Status & 0x0080) != 0, "acknowledge should not clear the visible V interrupt status bit");
    _ = vdp.StepScanline(Vdp.NtscScanlines - 1, pal: false);
    _ = vdp.ReadControlPort();
    AssertTrue(!vdp.VInterruptPending, "status read after VBlank should clear the V interrupt status latch");
}

void VdpHvCounterAdvancesHorizontally()
{
    Vdp vdp = new();
    vdp.BeginFrame(pal: false);
    ushort lineStart = vdp.ReadHvCounter(0, 3420);
    ushort middle = vdp.ReadHvCounter(1710, 3420);
    ushort lineEnd = vdp.ReadHvCounter(3419, 3420);

    AssertEqual((ushort)0x0084, lineStart);
    AssertTrue((middle & 0x00FF) != (lineStart & 0x00FF), "horizontal counter should advance inside a scanline");
    AssertTrue((lineEnd & 0x00FF) != (middle & 0x00FF), "horizontal counter should continue through the line");
    AssertEqual((ushort)0x0000, (ushort)(middle & 0xFF00));
}

void VdpHBlankStatusPulsesWithoutHInterrupt()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8A7F); // Keep the H-interrupt counter away from zero.
    vdp.BeginFrame(pal: false);

    Vdp.Interrupts interrupts = vdp.StepScanline(0, pal: false);
    AssertTrue((interrupts & Vdp.Interrupts.Horizontal) == 0, "test setup should not request H interrupt");
    AssertTrue((vdp.Status & 0x0004) == 0, "HBlank should start clear during the active scanline slice");

    vdp.SetHBlank(true);
    ushort status = vdp.ReadControlPort();
    AssertTrue((status & 0x0004) != 0, "HBlank status should pulse every scanline");
    AssertTrue((vdp.Status & 0x0004) != 0, "HBlank should remain set for the HBlank slice");
    vdp.SetHBlank(false);
    AssertTrue((vdp.Status & 0x0004) == 0, "HBlank should clear when the slice ends");
}

void VdpDmaMemoryCopyWritesVramAndCram()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Reset();
    machine.Bus.WriteWord(0x00FF_0000, 0x1234);
    machine.Bus.WriteWord(0x00FF_0002, 0x5678);
    List<DmaWordTransfer> transfers = [];
    machine.Bus.DmaWordObserver = transfers.Add;

    ConfigureDma(machine.Vdp, lengthWords: 2, sourceAddress: 0x00FF_0000, mode: 0);
    machine.Bus.WriteWord(0x00C0_0004, 0x4000); // VRAM DMA write at $0000.
    machine.Bus.WriteWord(0x00C0_0004, 0x0080);

    AssertEqual((byte)0x12, machine.Vdp.Vram[0]);
    AssertEqual((byte)0x34, machine.Vdp.Vram[1]);
    AssertEqual((byte)0x56, machine.Vdp.Vram[2]);
    AssertEqual((byte)0x78, machine.Vdp.Vram[3]);
    AssertEqual(1, machine.Vdp.DmaEvents.Count);
    AssertEqual("68k-to-vdp", machine.Vdp.DmaEvents[0].Operation);
    AssertEqual(2, transfers.Count);
    AssertEqual(0x00FF_0000u, transfers[0].SourceAddress);
    AssertEqual(0x0000u, transfers[0].DestinationAddress);
    AssertEqual(0x1234, transfers[0].Value);
    AssertEqual(0x00FF_0002u, transfers[1].SourceAddress);
    AssertEqual(0x0002u, transfers[1].DestinationAddress);
    AssertEqual(0x5678, transfers[1].Value);

    machine.Bus.WriteWord(0x00FF_0010, 0x000E);
    ConfigureDma(machine.Vdp, lengthWords: 1, sourceAddress: 0x00FF_0010, mode: 0);
    machine.Bus.WriteWord(0x00C0_0004, 0xC000); // CRAM DMA write at color 0.
    machine.Bus.WriteWord(0x00C0_0004, 0x0080);

    AssertEqual((ushort)0x000E, machine.Vdp.Cram[0]);
    AssertEqual(2, machine.Vdp.DmaEvents.Count);

    byte[] x32Rom = CreateRom();
    WriteAscii(x32Rom, 0x100, "SEGA 32X");
    MegaDrive x32Machine = new(CartridgeImage.FromBytes(x32Rom));
    x32Machine.Reset();
    x32Machine.Bus.WriteWord(0x00FF_0200, 0x1111);
    x32Machine.Bus.WriteWord(0x00FF_0202, 0x2222);
    x32Machine.Bus.WriteWord(0x00FF_0204, 0x3333);
    x32Machine.Bus.WriteWord(0x00FF_0206, 0x4444);
    x32Machine.Bus.WriteWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.DreqSourceAddressOffset), 0x00FF);
    x32Machine.Bus.WriteWord(ThirtyTwoXHardwareProfile.M68kSystemRegister((ushort)(ThirtyTwoXHardwareProfile.DreqSourceAddressOffset + 2)), 0x0200);
    x32Machine.Bus.WriteWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.DreqLengthOffset), 0x0004);
    x32Machine.Bus.WriteWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset), 0x0001);
    ConfigureDma(x32Machine.Vdp, lengthWords: 4, sourceAddress: 0x00FF_0200, mode: 0);
    x32Machine.Bus.WriteWord(0x00C0_0004, 0x4000);
    x32Machine.Bus.WriteWord(0x00C0_0004, 0x0080);

    AssertEqual((ushort)0x0000, x32Machine.Bus.ReadWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));
    AssertEqual((ushort)0x0000, x32Machine.Bus.ReadWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.DreqLengthOffset)));
    AssertEqual((ushort)0x1111, x32Machine.Bus.ReadWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.DreqFifoOffset)));
    AssertEqual((ushort)0x2222, x32Machine.Bus.ReadWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.DreqFifoOffset)));
    AssertEqual((ushort)0x3333, x32Machine.Bus.ReadWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.DreqFifoOffset)));
    AssertEqual((ushort)0x4444, x32Machine.Bus.ReadWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.DreqFifoOffset)));
    AssertEqual((ushort)0x0000, x32Machine.Bus.ReadWord(ThirtyTwoXHardwareProfile.M68kSystemRegister(ThirtyTwoXHardwareProfile.DreqControlOffset)));
}

void VdpLongDmaTimingScalesWithTransferLength()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Reset();

    const int lengthWords = 2_000;
    ConfigureDma(machine.Vdp, lengthWords, sourceAddress: 0x00FF_0000, mode: 0);
    machine.Bus.WriteWord(0x00C0_0004, 0x4000);
    machine.Bus.WriteWord(0x00C0_0004, 0x0080);

    AssertEqual(lengthWords * 2, machine.Vdp.DmaCycleDebt);
    AssertEqual(1, machine.Vdp.DmaEvents.Count);
    AssertEqual(lengthWords, machine.Vdp.DmaEvents[0].LengthWords);
}

void VdpDmaFillAndCopyModes()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Reset();

    ConfigureDma(machine.Vdp, lengthWords: 3, sourceAddress: 0, mode: 2);
    machine.Bus.WriteWord(0x00C0_0004, 0x4000); // VRAM DMA fill at $0000.
    machine.Bus.WriteWord(0x00C0_0004, 0x0080);
    machine.Bus.WriteWord(0x00C0_0000, 0xCAFE);

    AssertEqual((byte)0xCA, machine.Vdp.Vram[0]);
    AssertEqual((byte)0xFE, machine.Vdp.Vram[1]);
    AssertEqual((byte)0xFE, machine.Vdp.Vram[2]);
    AssertEqual((byte)0x00, machine.Vdp.Vram[3]);
    AssertEqual((byte)0xFE, machine.Vdp.Vram[4]);
    AssertEqual((byte)0x00, machine.Vdp.Vram[5]);
    AssertEqual((byte)0xFE, machine.Vdp.Vram[6]);
    AssertEqual(2, machine.Vdp.DmaEvents.Count);
    AssertEqual("fill-armed", machine.Vdp.DmaEvents[0].Operation);
    AssertEqual(6, machine.Vdp.DmaCycleDebt);

    WriteVramWordAt(machine.Vdp, 0x0000, 0xCAFE);
    WriteVramWordAt(machine.Vdp, 0x0002, 0xCAFE);

    ConfigureDma(machine.Vdp, lengthWords: 2, sourceAddress: 0, mode: 3);
    machine.Bus.WriteWord(0x00C0_0004, 0x4020); // VRAM DMA copy to $0020.
    machine.Bus.WriteWord(0x00C0_0004, 0x0080);

    AssertEqual((byte)0xCA, machine.Vdp.Vram[0x20]);
    AssertEqual((byte)0xFE, machine.Vdp.Vram[0x21]);
    AssertEqual((byte)0xCA, machine.Vdp.Vram[0x22]);
    AssertEqual((byte)0xFE, machine.Vdp.Vram[0x23]);
    AssertEqual("vram-copy", machine.Vdp.DmaEvents[^1].Operation);
}

void VdpDmaOnlyStartsOnCompletedCommand()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Reset();
    machine.Bus.WriteWord(0x00FF_0000, 0x1234);

    ConfigureDma(machine.Vdp, lengthWords: 1, sourceAddress: 0x00FF_0000, mode: 0);
    AssertEqual(0, machine.Vdp.DmaEvents.Count);

    machine.Bus.WriteWord(0x00C0_0004, 0x4000);
    AssertEqual(0, machine.Vdp.DmaEvents.Count);

    machine.Bus.WriteWord(0x00C0_0004, 0x0080);
    AssertEqual(1, machine.Vdp.DmaEvents.Count);
    AssertEqual((byte)0x12, machine.Vdp.Vram[0]);
    AssertEqual((byte)0x34, machine.Vdp.Vram[1]);
}

void VdpDataFifoAdds68kWaitCyclesWhenFull()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Reset();

    machine.Bus.WriteWord(0x00C0_0004, 0x4000);
    machine.Bus.WriteWord(0x00C0_0004, 0x0000);
    _ = machine.Bus.ConsumeM68kWaitCycles();

    machine.Bus.WriteWord(0x00C0_0000, 0x1111);
    machine.Bus.WriteWord(0x00C0_0000, 0x2222);
    machine.Bus.WriteWord(0x00C0_0000, 0x3333);
    machine.Bus.WriteWord(0x00C0_0000, 0x4444);
    AssertEqual(8, machine.Bus.ConsumeM68kWaitCycles());

    machine.Bus.WriteWord(0x00C0_0000, 0x5555);
    AssertEqual(6, machine.Bus.ConsumeM68kWaitCycles());
    AssertEqual(0, machine.Bus.ConsumeM68kWaitCycles());
}

void M68kPeripheralAccessesAddWaitCycles()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Reset();

    machine.Bus.WriteByte(0x00A1_0009, 0x40);
    AssertEqual(2, machine.Bus.ConsumeM68kWaitCycles());

    _ = machine.Bus.ReadByte(0x00A1_0001);
    AssertEqual(2, machine.Bus.ConsumeM68kWaitCycles());

    machine.Bus.WriteWord(0x00A0_0000, 0x1234);
    AssertEqual(2, machine.Bus.ConsumeM68kWaitCycles());

    _ = machine.Bus.ReadLong(0x00A0_0000);
    AssertEqual(4, machine.Bus.ConsumeM68kWaitCycles());

    machine.Bus.WriteByte(0x00A0_4000, 0x22);
    AssertEqual(2, machine.Bus.ConsumeM68kWaitCycles());

    machine.Bus.WriteWord(0x00C0_0004, 0x8F02);
    AssertEqual(2, machine.Bus.ConsumeM68kWaitCycles());

    machine.Bus.WriteByte(0x00A1_3000, 0x01);
    AssertEqual(2, machine.Bus.ConsumeM68kWaitCycles());
}

void ControllerMultiplexing()
{
    ThreeButtonController controller = new();
    controller.Pressed = GenesisButton.A | GenesisButton.B | GenesisButton.Start;

    controller.WriteControl(0x40);
    byte high = controller.ReadData();
    AssertTrue((high & 0x10) == 0, "B should be visible when TH is high");
    AssertTrue((high & 0x20) != 0, "Start should be hidden when TH is high");

    controller.WriteControl(0x00);
    byte low = controller.ReadData();
    AssertTrue((low & 0x04) == 0, "Left line should identify a 3-button controller when TH is low");
    AssertTrue((low & 0x08) == 0, "Right line should identify a 3-button controller when TH is low");
    AssertTrue((low & 0x10) == 0, "A should be visible when TH is low");
    AssertTrue((low & 0x20) == 0, "Start should be visible when TH is low");
}

void SixButtonControllerHandshake()
{
    ThreeButtonController controller = new()
    {
        SixButtonEnabled = true,
        Pressed = GenesisButton.A |
            GenesisButton.B |
            GenesisButton.C |
            GenesisButton.Start |
            GenesisButton.X |
            GenesisButton.Y |
            GenesisButton.Z |
            GenesisButton.Mode,
    };

    controller.WriteData(0x40, 10);
    byte firstHigh = controller.ReadData();
    AssertTrue((firstHigh & 0x10) == 0, "B should remain visible during the first high read");
    AssertTrue((firstHigh & 0x20) == 0, "C should remain visible during the first high read");

    controller.WriteData(0x00, 20);
    byte firstLow = controller.ReadData();
    AssertTrue((firstLow & 0x0C) == 0, "3-button signature should be visible before the six-button unlock");
    AssertTrue((firstLow & 0x10) == 0, "A should remain visible during the first low read");
    AssertTrue((firstLow & 0x20) == 0, "Start should remain visible during the first low read");

    controller.WriteData(0x40, 30);
    controller.WriteData(0x00, 40);
    controller.WriteData(0x40, 50);
    controller.WriteData(0x00, 60);
    byte signature = controller.ReadData();
    AssertEqual(0, signature & 0x0F);

    controller.WriteData(0x40, 70);
    byte extra = controller.ReadData();
    AssertTrue((extra & 0x01) == 0, "Z should be visible after the six-button unlock");
    AssertTrue((extra & 0x02) == 0, "Y should be visible after the six-button unlock");
    AssertTrue((extra & 0x04) == 0, "X should be visible after the six-button unlock");
    AssertTrue((extra & 0x08) == 0, "Mode should be visible after the six-button unlock");
    AssertTrue((extra & 0x10) == 0, "B should remain visible after the six-button unlock");
    AssertTrue((extra & 0x20) == 0, "C should remain visible after the six-button unlock");
    AssertTrue((extra & 0x40) != 0, "TH should read high during the extra-button phase");

    controller.WriteData(0x00, 200_000);
    controller.WriteData(0x40, 200_010);
    byte resetHigh = controller.ReadData();
    AssertTrue((resetHigh & 0x0F) != 0, "A long idle gap should reset the six-button handshake");
}

void ControllerDataAndControlPorts()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Reset();
    machine.Bus.Controller1.Pressed = GenesisButton.B | GenesisButton.Start;

    AssertEqual((uint)0, machine.Bus.ReadLong(0x00A1_0008));

    machine.Bus.WriteByte(0x00A1_0009, 0x40); // TH is an output, lower bits are controller inputs.
    machine.Bus.WriteByte(0x00A1_0003, 0x40);
    byte high = machine.Bus.ReadByte(0x00A1_0003);
    AssertTrue((high & 0x40) != 0, "TH output latch should read high");
    AssertTrue((high & 0x10) == 0, "B should be visible through the data port when TH is high");
    AssertTrue((high & 0x20) != 0, "Start should be hidden through the data port when TH is high");

    machine.Bus.WriteByte(0x00A1_0003, 0x00);
    byte low = machine.Bus.ReadByte(0x00A1_0003);
    AssertTrue((low & 0x40) == 0, "TH output latch should read low");
    AssertTrue((low & 0x04) == 0, "Left line should identify a 3-button controller through the data port when TH is low");
    AssertTrue((low & 0x08) == 0, "Right line should identify a 3-button controller through the data port when TH is low");
    AssertTrue((low & 0x20) == 0, "Start should be visible through the data port when TH is low");
}

void ControllerInputPinsDoNotDriveTh()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Reset();

    machine.Bus.WriteByte(0x00A1_000B, 0x39); // TH remains an input on port 2.
    machine.Bus.WriteByte(0x00A1_0005, 0x00);
    machine.Bus.WriteByte(0x00A1_0005, 0x09);

    byte value = machine.Bus.ReadByte(0x00A1_0005);
    AssertEqual(0x06, value & 0x06);
    AssertTrue((value & 0x40) != 0, "TH should remain pulled high when its control bit is input");
}

void SegaTeamPlayerAdapterProtocol()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Bus.Port1Device = ControllerPortDevice.SegaTeamPlayer;
    machine.Bus.Controller1.Pressed = GenesisButton.Up | GenesisButton.Right | GenesisButton.B | GenesisButton.Start;
    machine.Bus.Controller2.Pressed = GenesisButton.Down;
    machine.Bus.Controller3.Pressed = GenesisButton.Left;
    machine.Bus.Controller4.Pressed = GenesisButton.A;

    machine.Bus.WriteByte(0x00A1_0009, 0x60); // TH and TR output, lower data lines input.
    machine.Bus.WriteByte(0x00A1_0003, 0x60);
    AssertEqual(0x03, machine.Bus.ReadByte(0x00A1_0003) & 0x0F);

    TeamPlayerStep(machine, 0x20);
    AssertEqual(0x0F, machine.Bus.ReadByte(0x00A1_0003) & 0x0F);

    TeamPlayerStep(machine, 0x00);
    AssertEqual(0x00, machine.Bus.ReadByte(0x00A1_0003) & 0x0F);

    TeamPlayerStep(machine, 0x20);
    AssertEqual(0x00, machine.Bus.ReadByte(0x00A1_0003) & 0x0F);

    TeamPlayerStep(machine, 0x00);
    AssertEqual(0x00, machine.Bus.ReadByte(0x00A1_0003) & 0x0F);

    TeamPlayerStep(machine, 0x20);
    AssertEqual(0x00, machine.Bus.ReadByte(0x00A1_0003) & 0x0F);

    TeamPlayerStep(machine, 0x00);
    AssertEqual(0x00, machine.Bus.ReadByte(0x00A1_0003) & 0x0F);

    TeamPlayerStep(machine, 0x20);
    AssertEqual(0x00, machine.Bus.ReadByte(0x00A1_0003) & 0x0F);

    TeamPlayerStep(machine, 0x00);
    int p1Direction = machine.Bus.ReadByte(0x00A1_0003) & 0x0F;
    AssertTrue((p1Direction & 0x01) == 0, "Team Player should expose P1 Up in the first pad data nibble");
    AssertTrue((p1Direction & 0x08) == 0, "Team Player should expose P1 Right in the first pad data nibble");

    TeamPlayerStep(machine, 0x20);
    int p1Buttons = machine.Bus.ReadByte(0x00A1_0003) & 0x0F;
    AssertTrue((p1Buttons & 0x01) == 0, "Team Player should expose P1 B in the second pad data nibble");
    AssertTrue((p1Buttons & 0x08) == 0, "Team Player should expose P1 Start in the second pad data nibble");
}

void TeamPlayerStep(MegaDrive machine, byte value)
{
    machine.Bus.WriteByte(0x00A1_0003, value);
}

void Ea4WayPlayAdapterProtocol()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Bus.Port1Device = ControllerPortDevice.Ea4WayPlay;
    machine.Bus.Controller1.Pressed = GenesisButton.Up;
    machine.Bus.Controller2.Pressed = GenesisButton.Down;
    machine.Bus.Controller3.Pressed = GenesisButton.Left;
    machine.Bus.Controller4.Pressed = GenesisButton.Right;

    machine.Bus.WriteByte(0x00A1_0009, 0x40); // Port A TH output, data lines input.
    machine.Bus.WriteByte(0x00A1_000B, 0x33); // Port B TL/TR/TH/D0/D1 output.

    SelectEa4WayPlayController(machine, 0);
    AssertTrue((machine.Bus.ReadByte(0x00A1_0003) & 0x01) == 0, "4-Way Play should expose controller 1 on port A");

    SelectEa4WayPlayController(machine, 1);
    AssertTrue((machine.Bus.ReadByte(0x00A1_0003) & 0x02) == 0, "4-Way Play should expose controller 2 on port A");

    SelectEa4WayPlayController(machine, 2);
    AssertTrue((machine.Bus.ReadByte(0x00A1_0003) & 0x04) == 0, "4-Way Play should expose controller 3 on port A");

    SelectEa4WayPlayController(machine, 3);
    AssertTrue((machine.Bus.ReadByte(0x00A1_0003) & 0x08) == 0, "4-Way Play should expose controller 4 on port A");

    SelectEa4WayPlayController(machine, 4);
    AssertEqual(0x7C, machine.Bus.ReadByte(0x00A1_0003) & 0x7F);
    AssertEqual(0x7F, machine.Bus.ReadByte(0x00A1_0005) & 0x7F);
}

void SelectEa4WayPlayController(MegaDrive machine, int latch)
{
    machine.Bus.WriteByte(0x00A1_0005, (byte)((latch << 4) & 0x70));
}

void LightGunAdapterButtonProtocol()
{
    MegaDrive menacer = new(CartridgeImage.FromBytes(CreateRom()));
    menacer.Bus.Port2Device = ControllerPortDevice.Menacer;
    menacer.Bus.Controller2.Pressed = GenesisButton.A | GenesisButton.B | GenesisButton.C | GenesisButton.Start;
    byte menacerValue = menacer.Bus.ReadByte(0x00A1_0005);
    AssertEqual(0x4F, menacerValue & 0x7F);

    MegaDrive justifier = new(CartridgeImage.FromBytes(CreateRom()));
    justifier.Bus.Port2Device = ControllerPortDevice.KonamiJustifier;
    justifier.Bus.WriteByte(0x00A1_000B, 0x60);
    justifier.Bus.WriteByte(0x00A1_0005, 0x40);
    AssertEqual(0x30, justifier.Bus.ReadByte(0x00A1_0005) & 0x7F);

    justifier.Bus.Controller2.Pressed = GenesisButton.A | GenesisButton.Start;
    justifier.Bus.WriteByte(0x00A1_0005, 0x00);
    AssertEqual(0x70, justifier.Bus.ReadByte(0x00A1_0005) & 0x7C);
    AssertEqual(0x00, justifier.Bus.ReadByte(0x00A1_0005) & 0x03);

    justifier.Bus.Controller3.Pressed = GenesisButton.A;
    justifier.Bus.WriteByte(0x00A1_0005, 0x20);
    AssertEqual(0x00, justifier.Bus.ReadByte(0x00A1_0005) & 0x01);
    AssertEqual(0x02, justifier.Bus.ReadByte(0x00A1_0005) & 0x02);

    MegaDrive aiming = new(CartridgeImage.FromBytes(CreateRom()));
    aiming.Bus.Port2Device = ControllerPortDevice.Menacer;
    aiming.Bus.MasterCyclesPerScanline = 3_200;
    aiming.Bus.SetLightGunPosition(160, 40, visible: true);
    aiming.Bus.BeginLightGunFrame();
    aiming.Vdp.BeginFrame(pal: false);
    _ = aiming.Vdp.StepScanline(40, pal: false);
    aiming.Bus.UpdateLightGunForScanline(40);
    AssertTrue(!aiming.Bus.LightGunLatchedThisFrame, "Light gun should not latch unless port 2 HL control is enabled");

    aiming.Bus.WriteByte(0x00A1_000B, 0x80);
    aiming.Bus.BeginLightGunFrame();
    aiming.Vdp.BeginFrame(pal: false);
    _ = aiming.Vdp.StepScanline(40, pal: false);
    aiming.Bus.UpdateLightGunForScanline(40);
    ushort latchedHv = aiming.Bus.ReadWord(0x00C0_0008);
    AssertEqual(40, (latchedHv >> 8) & 0xFF);

    _ = aiming.Vdp.StepScanline(80, pal: false);
    aiming.Bus.UpdateLightGunForScanline(80);
    AssertTrue(!aiming.Bus.LightGunLatchedThisFrame, "Forced light gun latch should clear after the aimed scanline when VDP HV latch is disabled");

    aiming.Vdp.WriteControlPort(0x8002);
    aiming.Bus.BeginLightGunFrame();
    aiming.Vdp.BeginFrame(pal: false);
    _ = aiming.Vdp.StepScanline(40, pal: false);
    aiming.Bus.UpdateLightGunForScanline(40);
    latchedHv = aiming.Bus.ReadWord(0x00C0_0008);
    _ = aiming.Vdp.StepScanline(80, pal: false);
    aiming.Bus.UpdateLightGunForScanline(80);
    AssertEqual(latchedHv, aiming.Bus.ReadWord(0x00C0_0008));

    aiming.Bus.CurrentScanlineMasterCycleOffset = 1_600;
    _ = aiming.Vdp.StepScanline(40, pal: false);
    byte beamRead = aiming.Bus.ReadByte(0x00A1_0005);
    AssertTrue((beamRead & 0x40) == 0, "Menacer TH should go low when the beam reaches the aimed position");
}

void InputMoviesPreserveSeparatePlayerInputs()
{
    InputMovie movie = new();
    movie.AddFrame(0, GenesisButton.A | GenesisButton.Left, GenesisButton.Start | GenesisButton.Right);

    AssertEqual((int)(GenesisButton.A | GenesisButton.Left), (int)movie.GetButtons(0, playerIndex: 0));
    AssertEqual((int)(GenesisButton.Start | GenesisButton.Right), (int)movie.GetButtons(0, playerIndex: 1));
    AssertEqual((int)(GenesisButton.A | GenesisButton.Left), (int)movie.GetButtons(0));
}

void InputMoviesLoadLegacySinglePlayerFrames()
{
    string path = Path.Combine(Path.GetTempPath(), $"mdsharp-legacy-{Guid.NewGuid():N}.mdmovie");
    try
    {
        File.WriteAllText(path, """
            {
              "version": 1,
              "emulator": "mdSharp",
              "frames": [
                { "frame": 0, "buttons": 192 }
              ]
            }
            """);

        InputMovie movie = InputMovie.Load(path);
        AssertEqual((int)(GenesisButton.C | GenesisButton.Start), (int)movie.GetButtons(0, playerIndex: 0));
        AssertEqual((int)(GenesisButton.C | GenesisButton.Start), (int)movie.GetButtons(0, playerIndex: 1));
    }
    finally
    {
        File.Delete(path);
    }
}

void HardwareVersionRegisterReflectsRegion()
{
    byte[] rom = CreateRom();
    WriteAscii(rom, 0x1F0, "U");
    MegaDrive ntsc = new(CartridgeImage.FromBytes(rom), pal: false);
    AssertEqual((byte)0xA1, ntsc.Bus.ReadByte(0x00A1_0001));

    WriteAscii(rom, 0x1F0, "E");
    MegaDrive pal = new(CartridgeImage.FromBytes(rom), pal: true);
    AssertEqual((byte)0xE1, pal.Bus.ReadByte(0x00A1_0001));

    WriteAscii(rom, 0x1F0, "8");
    MegaDrive numericPal = new(CartridgeImage.FromBytes(rom), pal: true);
    AssertEqual((byte)0xE1, numericPal.Bus.ReadByte(0x00A1_0001));

    WriteAscii(rom, 0x1F0, "J");
    MegaDrive domestic = new(CartridgeImage.FromBytes(rom), pal: false);
    AssertEqual((byte)0x21, domestic.Bus.ReadByte(0x00A1_0001));
}

void PsgToneGeneration()
{
    Psg psg = new();
    psg.Write(0x80 | 0x04); // Channel 0 tone low bits.
    psg.Write(0x10); // Channel 0 tone high bits: period $104.
    psg.Write(0x90 | 0x00); // Channel 0 full volume.
    psg.Write(0xB0 | 0x0F); // Channel 1 muted.
    psg.Write(0xD0 | 0x0F); // Channel 2 muted.
    psg.Write(0xF0 | 0x0F); // Noise muted.

    AssertEqual(0x104, psg.TonePeriod(0));
    short[] samples = psg.RenderMonoSamples(128);
    AssertTrue(samples.Any(sample => sample != 0), "PSG tone should produce nonzero samples");
    AssertTrue(samples.Distinct().Count() > 1, "PSG tone should toggle waveform output");
}

void PsgResetStartsMuted()
{
    Psg psg = new();
    short[] samples = psg.RenderMonoSamples(128);
    AssertTrue(samples.All(sample => sample == 0), "PSG should power on muted until a game writes audible channel volumes");

    psg.Write(0x90 | 0x00);
    AssertTrue(psg.RenderMonoSamples(128).Any(sample => sample != 0), "PSG should still become audible after a volume write");
}

void PsgWritesRenderAtFrameTimestamps()
{
    Psg psg = new();
    psg.Write(0x9F); // Channel 0 muted before the frame starts.
    psg.Write(0xBF);
    psg.Write(0xDF);
    psg.Write(0xFF);

    psg.BeginAudioFrame(0, 100);
    psg.Write(0x80 | 0x02, 0);
    psg.Write(0x01, 0);
    psg.Write(0x90 | 0x00, 50);

    short[] samples = psg.RenderMonoSamples(10);
    AssertTrue(samples.Take(4).All(sample => Math.Abs(sample) < 512), "PSG volume write should not affect earlier frame samples");
    AssertTrue(samples.Skip(6).Any(sample => Math.Abs(sample) > 512), "PSG volume write should affect later frame samples");
}

void PsgFrameEventsAreSortedByTimestamp()
{
    Psg psg = new();
    psg.Write(0x80 | 0x02);
    psg.Write(0x01);
    psg.Write(0x9F);
    psg.Write(0xBF);
    psg.Write(0xDF);
    psg.Write(0xFF);

    psg.BeginAudioFrame(0, 100);
    psg.Write(0x9F, 80);
    psg.Write(0x90, 10);

    short[] samples = psg.RenderMonoSamples(10);
    AssertTrue(Math.Abs(samples[0]) < 512, "first sample should use the muted initial PSG state");
    AssertTrue(samples.Skip(1).Take(6).Any(sample => Math.Abs(sample) > 512), "earlier timestamped PSG write should render before later queued writes");
    AssertTrue(samples.Skip(8).All(sample => Math.Abs(sample) < 512), "later timestamped PSG write should mute the channel again");
}

void PsgChannelStemsIsolateToneChannel()
{
    Psg psg = new();
    psg.Write(0x80 | 0x04);
    psg.Write(0x10);
    psg.Write(0x90);
    psg.Write(0xBF);
    psg.Write(0xDF);
    psg.Write(0xFF);

    short[] stems = new short[4 * 128];
    psg.RenderMonoChannelStemsInto(stems, 128);

    AssertTrue(stems.Take(128).Any(sample => Math.Abs(sample) > 512), "PSG stem 0 should contain the keyed tone");
    AssertTrue(stems.Skip(128).All(sample => Math.Abs(sample) < 512), "muted PSG stems should stay quiet");
}

void PsgSnapshotsExposeToneState()
{
    Psg psg = new();
    psg.Write(0x80 | 0x04);
    psg.Write(0x10);
    psg.Write(0x90 | 0x03);
    psg.Write(0xBF);
    psg.Write(0xDF);
    psg.Write(0xE7);

    Psg.PsgChannelSnapshot[] snapshots = psg.GetChannelSnapshots();
    AssertEqual(4, snapshots.Length);
    AssertEqual(0x104, snapshots[0].Period);
    AssertEqual(3, snapshots[0].Volume);
    AssertTrue(snapshots[0].FrequencyHz > 400.0 && snapshots[0].FrequencyHz < 500.0, "PSG period $104 should report an audible tone frequency");
    AssertEqual(0x07, psg.GetNoiseSnapshot().Control);
}

void PsgNoiseControlResetsShiftRegister()
{
    Psg psg = new();
    psg.Write(0x9F);
    psg.Write(0xBF);
    psg.Write(0xDF);
    psg.Write(0xE4); // White noise, fixed low clock.
    psg.Write(0xF0); // Noise full volume.
    _ = psg.RenderMonoSamples(64);

    AssertTrue(psg.NoiseShift != 0x4000, "noise shift register should advance while rendering");
    psg.Write(0xE4);
    AssertEqual((ushort)0x4000, psg.NoiseShift);
}

void Ym2612TimersAndStatus()
{
    Ym2612 ym = new();
    ym.WriteAddress(0, 0x24);
    ym.WriteData(0, 0xFF);
    ym.WriteAddress(0, 0x25);
    ym.WriteData(0, 0x03);
    ym.WriteAddress(0, 0x27);
    ym.WriteData(0, 0x05); // Load/enable Timer A and enable overflow flag.

    ym.Step(144);
    AssertTrue((ym.ReadStatus() & 0x01) != 0, "Timer A should set status bit on overflow");

    ym.WriteAddress(0, 0x27);
    ym.WriteData(0, 0x10); // Reset Timer A flag.
    AssertTrue((ym.ReadStatus() & 0x01) == 0, "Timer A reset should clear status bit");

    ConfigureSimpleYmTone(ym, 0);
    WriteYmFrequency(ym, 0, 0x280, 4);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF0);
    short[] samples = ym.RenderMonoSamples(256);
    AssertTrue(samples.Distinct().Count() > 1, "keyed YM channel should produce a toggling placeholder tone");
}

void Ym2612TimerReloadsFromLatchWrites()
{
    Ym2612 ym = new();
    ym.WriteAddress(0, 0x26);
    ym.WriteData(0, 0xF0);
    ym.WriteAddress(0, 0x27);
    ym.WriteData(0, 0x0A); // Load/enable Timer B and enable overflow flag.

    ym.Step(30_000);
    ym.WriteAddress(0, 0x26);
    ym.WriteData(0, 0xE4); // Re-arm from the new latch while Timer B remains loaded.
    ym.Step(8_000);
    AssertTrue((ym.ReadStatus() & 0x02) == 0, "Timer B latch write should reload the running timer instead of keeping the old near-expired counter");

    ym.Step((256 - 0xE4) * 2_304);
    AssertTrue((ym.ReadStatus() & 0x02) != 0, "Timer B should set its status bit after the reloaded period elapses");
}

void Ym2612BusyStatusAfterRegisterWrites()
{
    Ym2612 ym = new();
    ym.WriteAddress(0, 0x22);
    ym.WriteData(0, 0x08, 1_000);

    AssertTrue((ym.ReadStatus(1_000) & 0x80) != 0, "YM should report busy immediately after a register write");
    AssertTrue((ym.ReadStatus(1_127) & 0x80) != 0, "YM should remain busy for the configured busy window");
    AssertTrue((ym.ReadStatus(1_128) & 0x80) == 0, "YM busy bit should clear after the busy window");
}

void Ym2612DacResetLevelIsNeutral()
{
    Ym2612 ym = new();
    ym.WriteAddress(0, 0x2B);
    ym.WriteData(0, 0x80);

    short[] samples = ym.RenderStereoSamples(128);
    AssertTrue(samples.All(sample => sample == 0), "Enabling DAC before the first sample write should output neutral silence");

    ym.WriteAddress(0, 0x2A);
    ym.WriteData(0, 0x00);
    AssertTrue(ym.RenderStereoSamples(128).Any(sample => sample < 0), "DAC should still produce a negative level after an explicit low sample write");
}

void Ym2612CsmKeyOnFromTimerAOverflow()
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 2);
    WriteYmFrequency(ym, 2, 0x280, 4);
    ym.WriteAddress(0, 0x24);
    ym.WriteData(0, 0xFF);
    ym.WriteAddress(0, 0x25);
    ym.WriteData(0, 0x03);
    ym.WriteAddress(0, 0x27);
    ym.WriteData(0, 0x85); // CSM, Timer A start, Timer A status enable.

    ym.Step(144);
    short[] samples = ym.RenderMonoSamples(256);
    AssertTrue((ym.ReadStatus() & 0x01) != 0, "Timer A should set status during CSM overflow");
    AssertTrue(samples.Any(sample => sample != 0), "CSM Timer A overflow should key on channel 3");
}

void Ym2612DacWritesRenderAtFrameTimestamps()
{
    Ym2612 ym = new();
    ym.WriteAddress(0, 0x2A);
    ym.WriteData(0, 0x80);
    ym.BeginAudioFrame(0, 100);
    ym.WriteAddress(0, 0x2B);
    ym.WriteData(0, 0x80, 0);
    ym.WriteAddress(0, 0x2A);
    ym.WriteData(0, 0xFF, 50);

    short[] samples = ym.RenderMonoSamples(10);
    AssertTrue(Math.Abs(samples[1]) < 256, "DAC should start from the frame's initial sample before timed writes");
    AssertTrue(samples[6] > 5_000, "DAC write should affect samples at and after its frame timestamp");
}

void Ym2612KeyOnWritesRenderAtFrameTimestamps()
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 0);
    WriteYmFrequency(ym, 0, 0x280, 4);

    ym.BeginAudioFrame(0, 640);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF0, 320);

    short[] samples = ym.RenderMonoSamples(64);
    AssertTrue(samples.Take(28).All(sample => Math.Abs(sample) < 128), "YM key-on should not affect earlier frame samples");
    AssertTrue(samples.Skip(40).Any(sample => Math.Abs(sample) > 32), "YM key-on should affect later frame samples");
}

void Ym2612FrameEventsAreSortedByTimestamp()
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 0);
    WriteYmFrequency(ym, 0, 0x280, 4);
    ym.WriteAddress(0, 0x40);
    ym.WriteData(0, 0x7F);
    ym.WriteAddress(0, 0x48);
    ym.WriteData(0, 0x7F);
    ym.WriteAddress(0, 0x44);
    ym.WriteData(0, 0x7F);
    ym.WriteAddress(0, 0x4C);
    ym.WriteData(0, 0x7F);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF0);

    ym.BeginAudioFrame(0, 100);
    ym.WriteAddress(0, 0x40);
    ym.WriteData(0, 0x7F, 80);
    ym.WriteAddress(0, 0x40);
    ym.WriteData(0, 0x00, 10);

    short[] samples = ym.RenderMonoSamples(10);
    AssertTrue(samples.Take(2).All(sample => Math.Abs(sample) < 64), "early samples should use the muted initial YM state");
    AssertTrue(samples.Skip(2).Take(5).Any(sample => Math.Abs(sample) > 8), "earlier timestamped YM write should render before later queued writes");
    AssertTrue(samples.Skip(9).All(sample => Math.Abs(sample) < 64), "later timestamped YM write should mute the operator again after the operator delay");
}

void Ym2612ChannelStemsIsolateKeyedChannel()
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 1);
    WriteYmFrequency(ym, 1, 0x280, 4);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF1);

    short[] stems = new short[6 * 128 * 2];
    ym.RenderStereoChannelStemsInto(stems, 128);

    int stem0Energy = StereoEnergy(stems, 0, 128);
    int stem1Energy = StereoEnergy(stems, 1, 128);
    int stem2Energy = StereoEnergy(stems, 2, 128);
    AssertEqual(0, stem0Energy);
    AssertTrue(stem1Energy > 0, "YM stem 1 should contain the keyed channel");
    AssertEqual(0, stem2Energy);
}

void Ym2612StereoPanning()
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 0);
    ym.WriteAddress(0, 0xB4);
    ym.WriteData(0, 0x80); // Channel 0 left only.
    WriteYmFrequency(ym, 0, 0x280, 4);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF0);

    short[] stereo = ym.RenderStereoSamples(256);
    int leftEnergy = 0;
    int rightEnergy = 0;
    for (int i = 0; i < stereo.Length; i += 2)
    {
        leftEnergy += Math.Abs(stereo[i]);
        rightEnergy += Math.Abs(stereo[i + 1]);
    }

    AssertTrue(leftEnergy > 0, "left-panned YM channel should render on the left");
    AssertEqual(0, rightEnergy);
}

void Ym2612RendersUpperBankKeyedChannels()
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 3);
    WriteYmFrequency(ym, 3, 0x290, 5);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF4); // Key on channel 4.

    short[] samples = ym.RenderMonoSamples(512);
    AssertTrue(samples.Any(sample => sample != 0), "upper-bank YM channel should produce audio");
    AssertTrue(samples.Distinct().Count() > 1, "upper-bank YM channel should vary over time");
}

void Ym2612Algorithm4UsesS2AndS4Carriers()
{
    Ym2612 mutedCarriers = CreateAlgorithmCarrierProbe(algorithm: 4, mutedOperators: [1, 3]);
    Ym2612 mutedModulators = CreateAlgorithmCarrierProbe(algorithm: 4, mutedOperators: [0, 2]);

    int carrierMutedEnergy = MonoEnergy(mutedCarriers.RenderMonoSamples(2048));
    int modulatorMutedEnergy = MonoEnergy(mutedModulators.RenderMonoSamples(2048));

    AssertTrue(modulatorMutedEnergy > 10_000, "algorithm 4 should keep output from S2/S4 carriers when S1/S3 modulators are muted");
    AssertTrue(carrierMutedEnergy * 8 < modulatorMutedEnergy, $"algorithm 4 should be mostly silent when S2/S4 carriers are muted ({carrierMutedEnergy} vs {modulatorMutedEnergy})");
}

void Ym2612Algorithm0UsesS4Carrier()
{
    Ym2612 mutedCarrier = CreateAlgorithmCarrierProbe(algorithm: 0, mutedOperators: [3]);
    Ym2612 mutedModulators = CreateAlgorithmCarrierProbe(algorithm: 0, mutedOperators: [0, 1, 2]);

    int carrierMutedEnergy = MonoEnergy(mutedCarrier.RenderMonoSamples(2048));
    int modulatorMutedEnergy = MonoEnergy(mutedModulators.RenderMonoSamples(2048));

    AssertTrue(modulatorMutedEnergy > 10_000, "algorithm 0 should keep output from S4 when S1/S2/S3 modulators are muted");
    AssertTrue(carrierMutedEnergy * 8 < modulatorMutedEnergy, $"algorithm 0 should be mostly silent when S4 is muted ({carrierMutedEnergy} vs {modulatorMutedEnergy})");
}

void Ym2612Algorithm5UsesS2S3AndS4Carriers()
{
    Ym2612 mutedCarriers = CreateAlgorithmCarrierProbe(algorithm: 5, mutedOperators: [1, 2, 3]);
    Ym2612 mutedModulator = CreateAlgorithmCarrierProbe(algorithm: 5, mutedOperators: [0]);

    int carrierMutedEnergy = MonoEnergy(mutedCarriers.RenderMonoSamples(2048));
    int modulatorMutedEnergy = MonoEnergy(mutedModulator.RenderMonoSamples(2048));

    AssertTrue(modulatorMutedEnergy > 10_000, "algorithm 5 should keep output from S2/S3/S4 when S1 is muted");
    AssertTrue(carrierMutedEnergy * 8 < modulatorMutedEnergy, $"algorithm 5 should be mostly silent when S2/S3/S4 carriers are muted ({carrierMutedEnergy} vs {modulatorMutedEnergy})");
}

void Ym2612SelectiveKeyOnMapsS2AndS3Bits()
{
    Ym2612 s2Keyed = CreateSelectiveKeyOnProbe(0x20);
    Ym2612 s3Keyed = CreateSelectiveKeyOnProbe(0x40);

    int s2Energy = MonoEnergy(s2Keyed.RenderMonoSamples(2048));
    int s3Energy = MonoEnergy(s3Keyed.RenderMonoSamples(2048));

    AssertTrue(s2Energy > 10_000, "key-on bit 5 should start S2, the algorithm 4 carrier");
    AssertTrue(s3Energy * 8 < s2Energy, $"key-on bit 6 should start S3, a muted modulator in this probe ({s3Energy} vs {s2Energy})");
}

void Ym2612SnapshotsExposeFeedbackAndModulationSensitivity()
{
    Ym2612 ym = new();
    ym.WriteAddress(0, 0xB0);
    ym.WriteData(0, 0x3A); // feedback 7, algorithm 2.
    ym.WriteAddress(0, 0xB4);
    ym.WriteData(0, 0xB5); // left, AMS 3, PMS 5.

    Ym2612.Ym2612ChannelSnapshot snapshot = ym.GetChannelSnapshots()[0];
    AssertEqual(2, snapshot.Algorithm);
    AssertEqual(7, snapshot.Feedback);
    AssertEqual(5, snapshot.PhaseModulationSensitivity);
    AssertEqual(3, snapshot.AmplitudeModulationSensitivity);

    ym.WriteAddress(0, 0x30);
    ym.WriteData(0, 0x73);
    snapshot = ym.GetChannelSnapshots()[0];
    AssertEqual(3, snapshot.Multipliers[0]);
    AssertEqual(7, snapshot.Detunes[0]);
}

void Ym2612FrequencyHighByteLatchesOnLowByteWrite()
{
    Ym2612 ym = new();
    WriteYmFrequency(ym, 0, 0x280, 4);

    ym.WriteAddress(0, 0xA4);
    ym.WriteData(0, 0x32);
    Ym2612.Ym2612ChannelSnapshot beforeLowWrite = ym.GetChannelSnapshots()[0];

    ym.WriteAddress(0, 0xA0);
    ym.WriteData(0, 0x90);
    Ym2612.Ym2612ChannelSnapshot afterLowWrite = ym.GetChannelSnapshots()[0];

    AssertEqual(0x280, beforeLowWrite.FNumber);
    AssertEqual(4, beforeLowWrite.Block);
    AssertEqual(0x290, afterLowWrite.FNumber);
    AssertEqual(6, afterLowWrite.Block);
}

void Ym2612Channel3SpecialModeAffectsOperatorPitch()
{
    Ym2612 normal = CreateChannel3SpecialProbe(enableSpecialMode: false);
    Ym2612 special = CreateChannel3SpecialProbe(enableSpecialMode: true);

    short[] normalSamples = normal.RenderMonoSamples(512);
    short[] specialSamples = special.RenderMonoSamples(512);
    AssertTrue(!normalSamples.SequenceEqual(specialSamples), "channel 3 special mode should alter the rendered operator pitch mix");
}

void Ym2612Channel3SpecialModeMapsOperatorFrequencyRegisters()
{
    Ym2612 lowS2 = CreateChannel3SingleOperatorProbe(s2Fnum: 0x280, s2Block: 3, s3Fnum: 0x700, s3Block: 5);
    Ym2612 highS2 = CreateChannel3SingleOperatorProbe(s2Fnum: 0x700, s2Block: 5, s3Fnum: 0x280, s3Block: 3);

    short[] lowSamples = lowS2.RenderMonoSamples(4096);
    short[] highSamples = highS2.RenderMonoSamples(4096);
    int lowCrossings = CountZeroCrossings(lowSamples);
    int highCrossings = CountZeroCrossings(highSamples);

    AssertTrue(highCrossings > lowCrossings * 3, $"operator S2 should use $AA/$AE in channel 3 special mode ({lowCrossings} vs {highCrossings} crossings)");
}

void Ym2612DetuneAffectsOperatorPitch()
{
    Ym2612 positive = CreateDetuneProbe(0x30);
    Ym2612 negative = CreateDetuneProbe(0x70);

    short[] positiveSamples = positive.RenderMonoSamples(4096);
    short[] negativeSamples = negative.RenderMonoSamples(4096);

    AssertTrue(!positiveSamples.SequenceEqual(negativeSamples), "positive and negative detune settings should produce different operator phase evolution");
}

void Ym2612DetuneIsAppliedBeforeMultiplier()
{
    int lowMultipleDifference = DetuneCrossingDifference(multiple: 1);
    int highMultipleDifference = DetuneCrossingDifference(multiple: 8);

    AssertTrue(highMultipleDifference > lowMultipleDifference * 2, $"detune should scale with operator multiple ({lowMultipleDifference} vs {highMultipleDifference})");
}

void Ym2612AttackRateZeroStaysSilent()
{
    Ym2612 ym = new();
    ym.WriteAddress(0, 0xB0);
    ym.WriteData(0, 0x07);
    WriteYmFrequency(ym, 0, 0x280, 4);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF0);

    short[] samples = ym.RenderMonoSamples(512);
    AssertTrue(samples.All(sample => sample == 0), "operators with attack rate zero should not rise from silence");
}

void Ym2612SsgEnvelopeCycles()
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 0);
    ym.WriteAddress(0, 0x60);
    ym.WriteData(0, 0x1F);
    ym.WriteAddress(0, 0x70);
    ym.WriteData(0, 0x1F);
    ym.WriteAddress(0, 0x90);
    ym.WriteData(0, 0x0B); // SSG-EG enabled, alternate with hold.
    WriteYmFrequency(ym, 0, 0x280, 4);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF0);

    _ = ym.RenderMonoSamples(4096);
    Ym2612.Ym2612State state = ym.CaptureState();
    AssertTrue(state.SsgHolding.Any(value => value), "SSG hold envelope should latch after cycling");
}

void Ym2612SustainLevelZeroRemainsAudible()
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 0);
    for (int op = 0; op < 4; op++)
    {
        int offset = YmOperatorRegisterOffset(op);
        ym.WriteAddress(0, (byte)(0x60 + offset));
        ym.WriteData(0, 0x1F);
        ym.WriteAddress(0, (byte)(0x80 + offset));
        ym.WriteData(0, 0x0F); // SL=0, RR=15.
    }

    WriteYmFrequency(ym, 0, 0x280, 4);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF0);

    short[] samples = ym.RenderMonoSamples(4096);
    AssertTrue(samples.Skip(2048).Any(sample => Math.Abs(sample) > 64), "SL=0 should sustain an audible operator level rather than decaying to silence");
}

void Ym2612TotalLevelAttenuationCanMuteCarriers()
{
    Ym2612 audible = new();
    ConfigureSimpleYmTone(audible, 0);
    WriteYmFrequency(audible, 0, 0x280, 4);
    audible.WriteAddress(0, 0x28);
    audible.WriteData(0, 0xF0);

    Ym2612 muted = new();
    ConfigureSimpleYmTone(muted, 0);
    for (int op = 0; op < 4; op++)
    {
        int offset = YmOperatorRegisterOffset(op);
        muted.WriteAddress(0, (byte)(0x40 + offset));
        muted.WriteData(0, 0x7F);
    }

    WriteYmFrequency(muted, 0, 0x280, 4);
    muted.WriteAddress(0, 0x28);
    muted.WriteData(0, 0xF0);

    int audibleEnergy = MonoEnergy(audible.RenderMonoSamples(2048));
    int mutedEnergy = MonoEnergy(muted.RenderMonoSamples(2048));
    AssertTrue(audibleEnergy > 10_000, "baseline carrier should be audible");
    AssertTrue(mutedEnergy * 16 < audibleEnergy, $"max total level should mute carriers ({mutedEnergy} vs {audibleEnergy})");
}

void Ym2612SustainLevelFifteenDecaysToSilence()
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 0);
    for (int op = 0; op < 4; op++)
    {
        int offset = YmOperatorRegisterOffset(op);
        ym.WriteAddress(0, (byte)(0x60 + offset));
        ym.WriteData(0, 0x1F);
        ym.WriteAddress(0, (byte)(0x80 + offset));
        ym.WriteData(0, 0xFF); // SL=15, RR=15.
    }

    WriteYmFrequency(ym, 0, 0x280, 4);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF0);

    short[] samples = ym.RenderMonoSamples(8192);
    AssertTrue(samples.Skip(6144).All(sample => Math.Abs(sample) < 32), "SL=15 should decay to the silent envelope floor");
}

void Ym2612LowSustainRatesDecayGradually()
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 0);
    for (int op = 0; op < 4; op++)
    {
        int offset = YmOperatorRegisterOffset(op);
        ym.WriteAddress(0, (byte)(0x60 + offset));
        ym.WriteData(0, 0x1F);
        ym.WriteAddress(0, (byte)(0x70 + offset));
        ym.WriteData(0, 0x01);
        ym.WriteAddress(0, (byte)(0x80 + offset));
        ym.WriteData(0, 0x0F); // SL=0, RR=15.
    }

    WriteYmFrequency(ym, 0, 0x280, 4);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF0);

    short[] samples = ym.RenderMonoSamples(4096);
    AssertTrue(samples.Skip(2048).Any(sample => Math.Abs(sample) > 64), "SR=1 should decay gradually, not force a one-unit envelope step every sample");
}

Ym2612 CreateChannel3SpecialProbe(bool enableSpecialMode)
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 2);
    ym.WriteAddress(0, 0x27);
    ym.WriteData(0, enableSpecialMode ? (byte)0x40 : (byte)0x00);
    ym.WriteAddress(0, 0xB2);
    ym.WriteData(0, 0x07); // Parallel algorithm.
    WriteYmFrequency(ym, 2, 0x040, 4);
    WriteChannel3Frequency(ym, 0xA8, 0xAC, 0x0E0, 5);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF2);
    _ = ym.RenderMonoSamples(64);
    return ym;
}

Ym2612 CreateChannel3SingleOperatorProbe(int s2Fnum, int s2Block, int s3Fnum, int s3Block)
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 2);
    ym.WriteAddress(0, 0x27);
    ym.WriteData(0, 0x40);
    ym.WriteAddress(0, 0xB2);
    ym.WriteData(0, 0x07);

    for (int op = 0; op < 4; op++)
    {
        int offset = YmOperatorRegisterOffset(op) + 2;
        ym.WriteAddress(0, (byte)(0x40 + offset));
        ym.WriteData(0, op == 1 ? (byte)0x00 : (byte)0x7F);
    }

    WriteChannel3Frequency(ym, 0xA2, 0xA6, 0x280, 3); // S4/base
    WriteChannel3Frequency(ym, 0xA8, 0xAC, s3Fnum, s3Block); // S3
    WriteChannel3Frequency(ym, 0xA9, 0xAD, 0x300, 4); // S1
    WriteChannel3Frequency(ym, 0xAA, 0xAE, s2Fnum, s2Block); // S2
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF2);
    _ = ym.RenderMonoSamples(512);
    return ym;
}

Ym2612 CreateDetuneProbe(byte detune)
{
    Ym2612 ym = new();
    ConfigureSimpleYmTone(ym, 0);
    for (int op = 0; op < 4; op++)
    {
        int offset = YmOperatorRegisterOffset(op);
        ym.WriteAddress(0, (byte)(0x30 + offset));
        ym.WriteData(0, (byte)(detune | 0x01));
    }

    WriteYmFrequency(ym, 0, 0x7F0, 5);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF0);
    _ = ym.RenderMonoSamples(512);
    return ym;
}

int DetuneCrossingDifference(byte multiple)
{
    Ym2612 plain = CreateSingleCarrierDetuneProbe(detune: 0x00, multiple);
    Ym2612 detuned = CreateSingleCarrierDetuneProbe(detune: 0x30, multiple);

    int plainCrossings = CountZeroCrossings(plain.RenderMonoSamples(32768));
    int detunedCrossings = CountZeroCrossings(detuned.RenderMonoSamples(32768));
    return Math.Abs(detunedCrossings - plainCrossings);
}

Ym2612 CreateSingleCarrierDetuneProbe(byte detune, byte multiple)
{
    Ym2612 ym = new();
    int bank = 0;
    int slot = 0;
    ym.WriteAddress(bank, (byte)(0xB0 + slot));
    ym.WriteData(bank, 0x07);

    for (int op = 0; op < 4; op++)
    {
        int offset = YmOperatorRegisterOffset(op) + slot;
        ym.WriteAddress(bank, (byte)(0x30 + offset));
        ym.WriteData(bank, (byte)(op == 3 ? detune | (multiple & 0x0F) : 0x01));
        ym.WriteAddress(bank, (byte)(0x40 + offset));
        ym.WriteData(bank, op == 3 ? (byte)0x00 : (byte)0x7F);
        ym.WriteAddress(bank, (byte)(0x50 + offset));
        ym.WriteData(bank, 0x1F);
        ym.WriteAddress(bank, (byte)(0x60 + offset));
        ym.WriteData(bank, 0x00);
        ym.WriteAddress(bank, (byte)(0x70 + offset));
        ym.WriteData(bank, 0x00);
        ym.WriteAddress(bank, (byte)(0x80 + offset));
        ym.WriteData(bank, 0x0F);
    }

    WriteYmFrequency(ym, 0, 0x7F0, 5);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, 0xF0);
    _ = ym.RenderMonoSamples(512);
    return ym;
}

void WriteChannel3Frequency(Ym2612 ym, byte lowRegister, byte highRegister, int fnum, int block)
{
    ym.WriteAddress(0, highRegister);
    ym.WriteData(0, (byte)(((block & 0x07) << 3) | ((fnum >> 8) & 0x07)));
    ym.WriteAddress(0, lowRegister);
    ym.WriteData(0, (byte)(fnum & 0xFF));
}

void WriteYmFrequency(Ym2612 ym, int channel, int fnum, int block)
{
    int bank = channel / 3;
    int slot = channel % 3;
    ym.WriteAddress(bank, (byte)(0xA4 + slot));
    ym.WriteData(bank, (byte)(((block & 0x07) << 3) | ((fnum >> 8) & 0x07)));
    ym.WriteAddress(bank, (byte)(0xA0 + slot));
    ym.WriteData(bank, (byte)(fnum & 0xFF));
}

int CountZeroCrossings(short[] samples)
{
    int crossings = 0;
    int previous = 0;
    foreach (short sample in samples)
    {
        int current = sample > 16 ? 1 : sample < -16 ? -1 : 0;
        if (current != 0 && previous != 0 && current != previous)
        {
            crossings++;
        }

        if (current != 0)
        {
            previous = current;
        }
    }

    return crossings;
}

int StereoEnergy(short[] channelMajorStereoStems, int channel, int samples)
{
    int start = channel * samples * 2;
    int energy = 0;
    for (int i = 0; i < samples * 2; i++)
    {
        energy += Math.Abs(channelMajorStereoStems[start + i]);
    }

    return energy;
}

int MonoEnergy(short[] samples)
{
    int energy = 0;
    foreach (short sample in samples)
    {
        energy += Math.Abs(sample);
    }

    return energy;
}

void ConfigureSimpleYmTone(Ym2612 ym, int channel)
{
    int bank = channel / 3;
    int slot = channel % 3;
    ym.WriteAddress(bank, (byte)(0xB0 + slot));
    ym.WriteData(bank, 0x07); // Parallel algorithm so all operators are audible in the placeholder core.

    for (int op = 0; op < 4; op++)
    {
        int offset = YmOperatorRegisterOffset(op) + slot;
        ym.WriteAddress(bank, (byte)(0x30 + offset));
        ym.WriteData(bank, 0x01);
        ym.WriteAddress(bank, (byte)(0x40 + offset));
        ym.WriteData(bank, 0x00);
        ym.WriteAddress(bank, (byte)(0x50 + offset));
        ym.WriteData(bank, 0x1F);
        ym.WriteAddress(bank, (byte)(0x60 + offset));
        ym.WriteData(bank, 0x00);
        ym.WriteAddress(bank, (byte)(0x70 + offset));
        ym.WriteData(bank, 0x00);
        ym.WriteAddress(bank, (byte)(0x80 + offset));
        ym.WriteData(bank, 0x0F);
    }
}

Ym2612 CreateAlgorithmCarrierProbe(int algorithm, int[] mutedOperators)
{
    Ym2612 ym = new();
    int channel = 0;
    int bank = 0;
    int slot = 0;

    ym.WriteAddress(bank, (byte)(0xB0 + slot));
    ym.WriteData(bank, (byte)(algorithm & 0x07));
    for (int op = 0; op < 4; op++)
    {
        int offset = YmOperatorRegisterOffset(op) + slot;
        ym.WriteAddress(bank, (byte)(0x30 + offset));
        ym.WriteData(bank, 0x01);
        ym.WriteAddress(bank, (byte)(0x40 + offset));
        ym.WriteData(bank, mutedOperators.Contains(op) ? (byte)0x7F : (byte)0x00);
        ym.WriteAddress(bank, (byte)(0x50 + offset));
        ym.WriteData(bank, 0x1F);
        ym.WriteAddress(bank, (byte)(0x60 + offset));
        ym.WriteData(bank, 0x00);
        ym.WriteAddress(bank, (byte)(0x70 + offset));
        ym.WriteData(bank, 0x00);
        ym.WriteAddress(bank, (byte)(0x80 + offset));
        ym.WriteData(bank, 0x0F);
    }

    ym.WriteAddress(bank, (byte)(0xA4 + slot));
    ym.WriteData(bank, 0x28);
    ym.WriteAddress(bank, (byte)(0xA0 + slot));
    ym.WriteData(bank, 0x80);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, (byte)(0xF0 | channel));
    _ = ym.RenderMonoSamples(256);
    return ym;
}

Ym2612 CreateSelectiveKeyOnProbe(byte keyOnValue)
{
    Ym2612 ym = new();
    int bank = 0;
    int slot = 0;

    ym.WriteAddress(bank, (byte)(0xB0 + slot));
    ym.WriteData(bank, 0x04); // S1->S2 and S3->S4, output from S2/S4.
    for (int op = 0; op < 4; op++)
    {
        int offset = YmOperatorRegisterOffset(op) + slot;
        ym.WriteAddress(bank, (byte)(0x30 + offset));
        ym.WriteData(bank, 0x01);
        ym.WriteAddress(bank, (byte)(0x40 + offset));
        ym.WriteData(bank, op == 1 ? (byte)0x00 : (byte)0x7F);
        ym.WriteAddress(bank, (byte)(0x50 + offset));
        ym.WriteData(bank, 0x1F);
        ym.WriteAddress(bank, (byte)(0x60 + offset));
        ym.WriteData(bank, 0x00);
        ym.WriteAddress(bank, (byte)(0x70 + offset));
        ym.WriteData(bank, 0x00);
        ym.WriteAddress(bank, (byte)(0x80 + offset));
        ym.WriteData(bank, 0x0F);
    }

    ym.WriteAddress(bank, (byte)(0xA4 + slot));
    ym.WriteData(bank, 0x28);
    ym.WriteAddress(bank, (byte)(0xA0 + slot));
    ym.WriteData(bank, 0x80);
    ym.WriteAddress(0, 0x28);
    ym.WriteData(0, keyOnValue);
    return ym;
}

int YmOperatorRegisterOffset(int op)
{
    return op switch
    {
        0 => 0x00,
        1 => 0x08,
        2 => 0x04,
        _ => 0x0C,
    };
}

void Z80CoreExecutesBasicProgram()
{
    byte[] memory = new byte[0x10000];
    memory[0x0000] = 0x3E; // LD A,$12
    memory[0x0001] = 0x12;
    memory[0x0002] = 0x06; // LD B,3
    memory[0x0003] = 0x03;
    memory[0x0004] = 0x04; // INC B
    memory[0x0005] = 0x80; // ADD A,B
    memory[0x0006] = 0x32; // LD ($2000),A
    memory[0x0007] = 0x00;
    memory[0x0008] = 0x20;
    memory[0x0009] = 0x76; // HALT

    Z80Core z80 = new();
    z80.Reset();
    z80.SetLines(resetAsserted: false, busRequested: false);
    while (!z80.Halted)
    {
        z80.StepInstruction(address => memory[address], (address, value) => memory[address] = value);
    }

    AssertEqual((byte)0x16, z80.A);
    AssertEqual((byte)0x16, memory[0x2000]);
}

void Z80CoreExecutesSonicBankInitLoop()
{
    byte[] memory = new byte[0x10000];
    byte[] program =
    [
        0xF3, 0xF3, 0xF3, 0x31, 0xFC, 0x1F, 0xDD, 0x21,
        0x00, 0x40, 0xAF, 0x32, 0xFD, 0x1F, 0x32, 0xFF,
        0x1F, 0x3E, 0x01, 0x32, 0x00, 0x60, 0x06, 0x08,
        0x3E, 0x07, 0x32, 0x00, 0x60, 0x0F, 0x10, 0xFA,
        0x76,
    ];
    Array.Copy(program, memory, program.Length);
    int bankWrites = 0;
    Z80Core z80 = new();
    z80.Reset();
    z80.SetLines(resetAsserted: false, busRequested: false);

    int guard = 0;
    while (!z80.Halted && guard++ < 64)
    {
        z80.StepInstruction(address => memory[address], (address, value) =>
        {
            if (address == 0x6000)
            {
                bankWrites++;
                return;
            }

            memory[address] = value;
        });
    }

    AssertTrue(z80.Halted, "Sonic Z80 bank-init loop should finish");
    AssertEqual((byte)0x00, z80.B);
    AssertEqual(9, bankWrites);
}

void Z80JrUsesDisplacementAfterOperand()
{
    byte[] memory = new byte[0x10000];
    memory[0x0000] = 0x18; // JR +2 should land at $0004, after the displacement operand.
    memory[0x0001] = 0x02;
    memory[0x0002] = 0x76; // Incorrect off-by-one target.
    memory[0x0003] = 0x76;
    memory[0x0004] = 0x3E; // LD A,$42
    memory[0x0005] = 0x42;
    memory[0x0006] = 0x76; // HALT

    Z80Core z80 = new();
    z80.Reset();
    z80.SetLines(resetAsserted: false, busRequested: false);
    int guard = 0;
    while (!z80.Halted && guard++ < 8)
    {
        z80.StepInstruction(address => memory[address], (address, value) => memory[address] = value);
    }

    AssertEqual((byte)0x42, z80.A);
}

void Z80CoreExecutesCbStackAndConditionalFlow()
{
    byte[] memory = new byte[0x10000];
    memory[0x0000] = 0x21; // LD HL,$2000
    memory[0x0001] = 0x00;
    memory[0x0002] = 0x20;
    memory[0x0003] = 0x36; // LD (HL),$80
    memory[0x0004] = 0x80;
    memory[0x0005] = 0xCB; // RLC (HL) => $01 with carry.
    memory[0x0006] = 0x06;
    memory[0x0007] = 0xCB; // BIT 0,(HL)
    memory[0x0008] = 0x46;
    memory[0x0009] = 0x7E; // LD A,(HL)
    memory[0x000A] = 0xE5; // PUSH HL
    memory[0x000B] = 0xD1; // POP DE
    memory[0x000C] = 0xFE; // CP $01
    memory[0x000D] = 0x01;
    memory[0x000E] = 0xCC; // CALL Z,$0018
    memory[0x000F] = 0x18;
    memory[0x0010] = 0x00;
    memory[0x0011] = 0x76; // HALT
    memory[0x0018] = 0x06; // LD B,3
    memory[0x0019] = 0x03;
    memory[0x001A] = 0x10; // DJNZ $001A
    memory[0x001B] = 0xFE;
    memory[0x001C] = 0xC9; // RET

    Z80Core z80 = new();
    z80.Reset();
    z80.SetLines(resetAsserted: false, busRequested: false);
    int guard = 0;
    while (!z80.Halted && guard++ < 64)
    {
        z80.StepInstruction(address => memory[address], (address, value) => memory[address] = value);
    }

    AssertTrue(z80.Halted, "Z80 should halt after returning from conditional call");
    AssertEqual((byte)0x01, memory[0x2000]);
    AssertEqual((byte)0x01, z80.A);
    AssertEqual((byte)0x20, z80.D);
    AssertEqual((byte)0x00, z80.E);
    AssertEqual((byte)0x00, z80.B);
}

void Z80BusWritesYm2612()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Reset();
    machine.Bus.WriteZ80Byte(0x0000, 0x3E); // LD A,$2A
    machine.Bus.WriteZ80Byte(0x0001, 0x2A);
    machine.Bus.WriteZ80Byte(0x0002, 0x32); // LD ($4000),A
    machine.Bus.WriteZ80Byte(0x0003, 0x00);
    machine.Bus.WriteZ80Byte(0x0004, 0x40);
    machine.Bus.WriteZ80Byte(0x0005, 0x3E); // LD A,$7F
    machine.Bus.WriteZ80Byte(0x0006, 0x7F);
    machine.Bus.WriteZ80Byte(0x0007, 0x32); // LD ($4001),A
    machine.Bus.WriteZ80Byte(0x0008, 0x01);
    machine.Bus.WriteZ80Byte(0x0009, 0x40);
    machine.Bus.WriteZ80Byte(0x000A, 0x76); // HALT

    machine.Z80.SetLines(resetAsserted: false, busRequested: false);
    while (!machine.Z80.Halted)
    {
        machine.Z80.StepInstruction(machine.Bus.ReadZ80Byte, machine.Bus.WriteZ80Byte);
    }

    AssertEqual((byte)0x7F, machine.Ym2612.ReadRegister(0, 0x2A));
}

void Z80CoreExecutesRegisterAluVariants()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Bus.WriteZ80Byte(0x0000, 0x21); // LD HL,$0100
    machine.Bus.WriteZ80Byte(0x0001, 0x00);
    machine.Bus.WriteZ80Byte(0x0002, 0x01);
    machine.Bus.WriteZ80Byte(0x0003, 0x36); // LD (HL),$22
    machine.Bus.WriteZ80Byte(0x0004, 0x22);
    machine.Bus.WriteZ80Byte(0x0005, 0x3E); // LD A,$22
    machine.Bus.WriteZ80Byte(0x0006, 0x22);
    machine.Bus.WriteZ80Byte(0x0007, 0xBE); // CP (HL)
    machine.Bus.WriteZ80Byte(0x0008, 0x28); // JR Z,+2
    machine.Bus.WriteZ80Byte(0x0009, 0x02);
    machine.Bus.WriteZ80Byte(0x000A, 0x3E); // Would load failure value if CP/JR failed.
    machine.Bus.WriteZ80Byte(0x000B, 0x00);
    machine.Bus.WriteZ80Byte(0x000C, 0x06); // LD B,$11
    machine.Bus.WriteZ80Byte(0x000D, 0x11);
    machine.Bus.WriteZ80Byte(0x000E, 0x80); // ADD A,B
    machine.Bus.WriteZ80Byte(0x000F, 0x76); // HALT

    machine.Z80.SetLines(resetAsserted: false, busRequested: false);
    while (!machine.Z80.Halted)
    {
        machine.Z80.StepInstruction(machine.Bus.ReadZ80Byte, machine.Bus.WriteZ80Byte);
    }

    AssertEqual((byte)0x33, machine.Z80.A);
}

void Z80AccumulatorFlagInstructions()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Bus.WriteZ80Byte(0x0000, 0x3E); // LD A,$45
    machine.Bus.WriteZ80Byte(0x0001, 0x45);
    machine.Bus.WriteZ80Byte(0x0002, 0xC6); // ADD A,$38 => $7D
    machine.Bus.WriteZ80Byte(0x0003, 0x38);
    machine.Bus.WriteZ80Byte(0x0004, 0x27); // DAA => $83
    machine.Bus.WriteZ80Byte(0x0005, 0x37); // SCF
    machine.Bus.WriteZ80Byte(0x0006, 0x3F); // CCF
    machine.Bus.WriteZ80Byte(0x0007, 0x2F); // CPL => $7C
    machine.Bus.WriteZ80Byte(0x0008, 0x76); // HALT

    machine.Z80.SetLines(resetAsserted: false, busRequested: false);
    while (!machine.Z80.Halted)
    {
        machine.Z80.StepInstruction(machine.Bus.ReadZ80Byte, machine.Bus.WriteZ80Byte);
    }

    AssertEqual((byte)0x7C, machine.Z80.A);
    AssertTrue((machine.Z80.F & 0x03) == 0x02, "CPL should set N and leave carry clear after CCF");
    AssertTrue((machine.Z80.F & 0x10) != 0, "CPL should set H");
}

void Z80MaskableInterruptEntersIm1VectorAfterEiDelay()
{
    byte[] memory = new byte[0x10000];
    memory[0x0000] = 0xED; // IM 1
    memory[0x0001] = 0x56;
    memory[0x0002] = 0xFB; // EI
    memory[0x0003] = 0x3E; // LD A,$11, must execute before IRQ is accepted.
    memory[0x0004] = 0x11;
    memory[0x0038] = 0x3E; // LD A,$42
    memory[0x0039] = 0x42;
    memory[0x003A] = 0xF3; // DI, then HALT so the level interrupt does not re-enter.
    memory[0x003B] = 0x76;

    Z80Core z80 = new();
    z80.Reset();
    z80.SetLines(resetAsserted: false, busRequested: false);
    z80.StepInstruction(address => memory[address], (address, value) => memory[address] = value, interruptPending: true);
    z80.StepInstruction(address => memory[address], (address, value) => memory[address] = value, interruptPending: true);
    z80.StepInstruction(address => memory[address], (address, value) => memory[address] = value, interruptPending: true);
    AssertEqual((byte)0x11, z80.A);
    z80.StepInstruction(address => memory[address], (address, value) => memory[address] = value, interruptPending: true);
    AssertEqual((ushort)0x0038, z80.PC);

    while (!z80.Halted)
    {
        z80.StepInstruction(address => memory[address], (address, value) => memory[address] = value, interruptPending: true);
    }

    AssertEqual((byte)0x42, z80.A);
    AssertEqual((byte)0x05, memory[0xFFFD]);
    AssertEqual((byte)0x00, memory[0xFFFE]);
}

void Z80MaskableInterruptSupportsIm2Vectors()
{
    byte[] memory = new byte[0x10000];
    memory[0x0000] = 0x3E; // LD A,$80
    memory[0x0001] = 0x80;
    memory[0x0002] = 0xED; // LD I,A
    memory[0x0003] = 0x47;
    memory[0x0004] = 0xED; // IM 2
    memory[0x0005] = 0x5E;
    memory[0x0006] = 0xFB; // EI
    memory[0x0007] = 0x00; // NOP before interrupt acceptance.
    memory[0x80FF] = 0x34;
    memory[0x8100] = 0x12;
    memory[0x1234] = 0x3E; // LD A,$69
    memory[0x1235] = 0x69;
    memory[0x1236] = 0x76;

    Z80Core z80 = new();
    z80.Reset();
    z80.SetLines(resetAsserted: false, busRequested: false);
    for (int i = 0; i < 6; i++)
    {
        z80.StepInstruction(address => memory[address], (address, value) => memory[address] = value, interruptPending: true);
    }

    AssertEqual((ushort)0x1234, z80.PC);
    while (!z80.Halted)
    {
        z80.StepInstruction(address => memory[address], (address, value) => memory[address] = value);
    }

    AssertEqual((byte)0x69, z80.A);
}

void Z80CoreExecutesEdAndIndexedOperations()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Bus.WriteZ80Byte(0x0000, 0xFD); // LD IY,$0100
    machine.Bus.WriteZ80Byte(0x0001, 0x21);
    machine.Bus.WriteZ80Byte(0x0002, 0x00);
    machine.Bus.WriteZ80Byte(0x0003, 0x01);
    machine.Bus.WriteZ80Byte(0x0004, 0xFD); // LD (IY+2),$44
    machine.Bus.WriteZ80Byte(0x0005, 0x36);
    machine.Bus.WriteZ80Byte(0x0006, 0x02);
    machine.Bus.WriteZ80Byte(0x0007, 0x44);
    machine.Bus.WriteZ80Byte(0x0008, 0xFD); // LD A,(IY+2)
    machine.Bus.WriteZ80Byte(0x0009, 0x7E);
    machine.Bus.WriteZ80Byte(0x000A, 0x02);
    machine.Bus.WriteZ80Byte(0x000B, 0xFD); // SET 0,(IY+2)
    machine.Bus.WriteZ80Byte(0x000C, 0xCB);
    machine.Bus.WriteZ80Byte(0x000D, 0x02);
    machine.Bus.WriteZ80Byte(0x000E, 0xC6);
    machine.Bus.WriteZ80Byte(0x000F, 0x76); // HALT

    machine.Z80.SetLines(resetAsserted: false, busRequested: false);
    while (!machine.Z80.Halted)
    {
        machine.Z80.StepInstruction(machine.Bus.ReadZ80Byte, machine.Bus.WriteZ80Byte);
    }

    AssertEqual((ushort)0x0100, machine.Z80.IY);
    AssertEqual((byte)0x44, machine.Z80.A);
    AssertEqual((byte)0x45, machine.Bus.ReadZ80Byte(0x0102));

    MegaDrive indexHalves = new(CartridgeImage.FromBytes(CreateRom()));
    indexHalves.Bus.WriteZ80Byte(0x0000, 0xDD); // LD IX,$1234
    indexHalves.Bus.WriteZ80Byte(0x0001, 0x21);
    indexHalves.Bus.WriteZ80Byte(0x0002, 0x34);
    indexHalves.Bus.WriteZ80Byte(0x0003, 0x12);
    indexHalves.Bus.WriteZ80Byte(0x0004, 0xDD); // LD E,IXL
    indexHalves.Bus.WriteZ80Byte(0x0005, 0x5D);
    indexHalves.Bus.WriteZ80Byte(0x0006, 0xDD); // LD D,IXH
    indexHalves.Bus.WriteZ80Byte(0x0007, 0x54);
    indexHalves.Bus.WriteZ80Byte(0x0008, 0xDD); // LD IXH,$56
    indexHalves.Bus.WriteZ80Byte(0x0009, 0x26);
    indexHalves.Bus.WriteZ80Byte(0x000A, 0x56);
    indexHalves.Bus.WriteZ80Byte(0x000B, 0xDD); // LD IXL,$78
    indexHalves.Bus.WriteZ80Byte(0x000C, 0x2E);
    indexHalves.Bus.WriteZ80Byte(0x000D, 0x78);
    indexHalves.Bus.WriteZ80Byte(0x000E, 0xDD); // LD B,IXH
    indexHalves.Bus.WriteZ80Byte(0x000F, 0x44);
    indexHalves.Bus.WriteZ80Byte(0x0010, 0xDD); // LD C,IXL
    indexHalves.Bus.WriteZ80Byte(0x0011, 0x4D);
    indexHalves.Bus.WriteZ80Byte(0x0012, 0xDD); // ADD A,IXH
    indexHalves.Bus.WriteZ80Byte(0x0013, 0x84);
    indexHalves.Bus.WriteZ80Byte(0x0014, 0xFD); // LD IY,$9ABC
    indexHalves.Bus.WriteZ80Byte(0x0015, 0x21);
    indexHalves.Bus.WriteZ80Byte(0x0016, 0xBC);
    indexHalves.Bus.WriteZ80Byte(0x0017, 0x9A);
    indexHalves.Bus.WriteZ80Byte(0x0018, 0xFD); // LD D,IYH
    indexHalves.Bus.WriteZ80Byte(0x0019, 0x54);
    indexHalves.Bus.WriteZ80Byte(0x001A, 0xFD); // LD E,IYL
    indexHalves.Bus.WriteZ80Byte(0x001B, 0x5D);
    indexHalves.Bus.WriteZ80Byte(0x001C, 0x76);
    indexHalves.Z80.SetLines(resetAsserted: false, busRequested: false);
    while (!indexHalves.Z80.Halted)
    {
        indexHalves.Z80.StepInstruction(indexHalves.Bus.ReadZ80Byte, indexHalves.Bus.WriteZ80Byte);
    }

    AssertEqual((byte)0x9A, indexHalves.Z80.D);
    AssertEqual((byte)0xBC, indexHalves.Z80.E);
    AssertEqual((ushort)0x5678, indexHalves.Z80.IX);
    AssertEqual((byte)0x56, indexHalves.Z80.B);
    AssertEqual((byte)0x78, indexHalves.Z80.C);
    AssertEqual((byte)0x56, indexHalves.Z80.A);

    MegaDrive indexedMemory = new(CartridgeImage.FromBytes(CreateRom()));
    indexedMemory.Bus.WriteZ80Byte(0x0000, 0xDD); // LD IX,$0100
    indexedMemory.Bus.WriteZ80Byte(0x0001, 0x21);
    indexedMemory.Bus.WriteZ80Byte(0x0002, 0x00);
    indexedMemory.Bus.WriteZ80Byte(0x0003, 0x01);
    indexedMemory.Bus.WriteZ80Byte(0x0004, 0x21); // LD HL,$ABCD
    indexedMemory.Bus.WriteZ80Byte(0x0005, 0xCD);
    indexedMemory.Bus.WriteZ80Byte(0x0006, 0xAB);
    indexedMemory.Bus.WriteZ80Byte(0x0007, 0xDD); // LD (IX+4),H
    indexedMemory.Bus.WriteZ80Byte(0x0008, 0x74);
    indexedMemory.Bus.WriteZ80Byte(0x0009, 0x04);
    indexedMemory.Bus.WriteZ80Byte(0x000A, 0xDD); // LD (IX+5),L
    indexedMemory.Bus.WriteZ80Byte(0x000B, 0x75);
    indexedMemory.Bus.WriteZ80Byte(0x000C, 0x05);
    indexedMemory.Bus.WriteZ80Byte(0x000D, 0x21); // LD HL,$0000
    indexedMemory.Bus.WriteZ80Byte(0x000E, 0x00);
    indexedMemory.Bus.WriteZ80Byte(0x000F, 0x00);
    indexedMemory.Bus.WriteZ80Byte(0x0010, 0xDD); // LD H,(IX+4)
    indexedMemory.Bus.WriteZ80Byte(0x0011, 0x66);
    indexedMemory.Bus.WriteZ80Byte(0x0012, 0x04);
    indexedMemory.Bus.WriteZ80Byte(0x0013, 0xDD); // LD L,(IX+5)
    indexedMemory.Bus.WriteZ80Byte(0x0014, 0x6E);
    indexedMemory.Bus.WriteZ80Byte(0x0015, 0x05);
    indexedMemory.Bus.WriteZ80Byte(0x0016, 0x76);
    indexedMemory.Z80.SetLines(resetAsserted: false, busRequested: false);
    while (!indexedMemory.Z80.Halted)
    {
        indexedMemory.Z80.StepInstruction(indexedMemory.Bus.ReadZ80Byte, indexedMemory.Bus.WriteZ80Byte);
    }

    AssertEqual((ushort)0x0100, indexedMemory.Z80.IX);
    AssertEqual((byte)0xAB, indexedMemory.Z80.H);
    AssertEqual((byte)0xCD, indexedMemory.Z80.L);
    AssertEqual((byte)0xAB, indexedMemory.Bus.ReadZ80Byte(0x0104));
    AssertEqual((byte)0xCD, indexedMemory.Bus.ReadZ80Byte(0x0105));

    MegaDrive exchange = new(CartridgeImage.FromBytes(CreateRom()));
    exchange.Bus.WriteZ80Byte(0x0000, 0x06); // LD B,$12
    exchange.Bus.WriteZ80Byte(0x0001, 0x12);
    exchange.Bus.WriteZ80Byte(0x0002, 0xD9); // EXX
    exchange.Bus.WriteZ80Byte(0x0003, 0x06); // LD B,$A0 in alternate set
    exchange.Bus.WriteZ80Byte(0x0004, 0xA0);
    exchange.Bus.WriteZ80Byte(0x0005, 0xD9); // EXX back
    exchange.Bus.WriteZ80Byte(0x0006, 0x76);
    exchange.Z80.SetLines(resetAsserted: false, busRequested: false);
    while (!exchange.Z80.Halted)
    {
        exchange.Z80.StepInstruction(exchange.Bus.ReadZ80Byte, exchange.Bus.WriteZ80Byte);
    }

    AssertEqual((byte)0x12, exchange.Z80.B);
    AssertEqual((byte)0xA0, exchange.Z80.AlternateB);

    MegaDrive arithmetic = new(CartridgeImage.FromBytes(CreateRom()));
    arithmetic.Bus.WriteZ80Byte(0x0000, 0x21); // LD HL,$0005
    arithmetic.Bus.WriteZ80Byte(0x0001, 0x05);
    arithmetic.Bus.WriteZ80Byte(0x0002, 0x00);
    arithmetic.Bus.WriteZ80Byte(0x0003, 0x11); // LD DE,$0003
    arithmetic.Bus.WriteZ80Byte(0x0004, 0x03);
    arithmetic.Bus.WriteZ80Byte(0x0005, 0x00);
    arithmetic.Bus.WriteZ80Byte(0x0006, 0xED); // SBC HL,DE
    arithmetic.Bus.WriteZ80Byte(0x0007, 0x52);
    arithmetic.Bus.WriteZ80Byte(0x0008, 0x76);
    arithmetic.Z80.SetLines(resetAsserted: false, busRequested: false);
    while (!arithmetic.Z80.Halted)
    {
        arithmetic.Z80.StepInstruction(arithmetic.Bus.ReadZ80Byte, arithmetic.Bus.WriteZ80Byte);
    }

    AssertEqual((byte)0x00, arithmetic.Z80.H);
    AssertEqual((byte)0x02, arithmetic.Z80.L);

    MegaDrive block = new(CartridgeImage.FromBytes(CreateRom()));
    block.Bus.WriteZ80Byte(0x0200, 0xAA);
    block.Bus.WriteZ80Byte(0x0201, 0xBB);
    block.Bus.WriteZ80Byte(0x0202, 0xCC);
    block.Bus.WriteZ80Byte(0x0000, 0xED); // IM 1
    block.Bus.WriteZ80Byte(0x0001, 0x56);
    block.Bus.WriteZ80Byte(0x0002, 0x21); // LD HL,$0200
    block.Bus.WriteZ80Byte(0x0003, 0x00);
    block.Bus.WriteZ80Byte(0x0004, 0x02);
    block.Bus.WriteZ80Byte(0x0005, 0x11); // LD DE,$0300
    block.Bus.WriteZ80Byte(0x0006, 0x00);
    block.Bus.WriteZ80Byte(0x0007, 0x03);
    block.Bus.WriteZ80Byte(0x0008, 0x01); // LD BC,$0003
    block.Bus.WriteZ80Byte(0x0009, 0x03);
    block.Bus.WriteZ80Byte(0x000A, 0x00);
    block.Bus.WriteZ80Byte(0x000B, 0xED); // LDIR
    block.Bus.WriteZ80Byte(0x000C, 0xB0);
    block.Bus.WriteZ80Byte(0x000D, 0x76);
    block.Z80.SetLines(resetAsserted: false, busRequested: false);
    while (!block.Z80.Halted)
    {
        block.Z80.StepInstruction(block.Bus.ReadZ80Byte, block.Bus.WriteZ80Byte);
    }

    AssertEqual((byte)0xAA, block.Bus.ReadZ80Byte(0x0300));
    AssertEqual((byte)0xBB, block.Bus.ReadZ80Byte(0x0301));
    AssertEqual((byte)0xCC, block.Bus.ReadZ80Byte(0x0302));
    AssertEqual((byte)0x02, block.Z80.H);
    AssertEqual((byte)0x03, block.Z80.L);
    AssertEqual((byte)0x03, block.Z80.D);
    AssertEqual((byte)0x03, block.Z80.E);
    AssertEqual((byte)0x00, block.Z80.B);
    AssertEqual((byte)0x00, block.Z80.C);
}

void Z80BusExposesBanked68kWindow()
{
    byte[] rom = CreateRom();
    rom[0x0000] = 0x12;
    rom[0x8000] = 0x34;
    MegaDrive machine = new(CartridgeImage.FromBytes(rom));

    machine.Bus.WriteZ80Byte(0x2000, 0xA5);
    AssertEqual((byte)0xA5, machine.Bus.ReadZ80Byte(0x0000));
    AssertEqual((byte)0x12, machine.Bus.ReadZ80Byte(0x8000));

    machine.Bus.WriteZ80Byte(0x6000, 0x01);
    for (int i = 0; i < 8; i++)
    {
        machine.Bus.WriteZ80Byte(0x6000, 0x00);
    }

    AssertEqual((byte)0x34, machine.Bus.ReadZ80Byte(0x8000));
}

void Z80NegAliasesUpdateFlags()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Bus.WriteZ80Byte(0x0000, 0x3E); // LD A,$34
    machine.Bus.WriteZ80Byte(0x0001, 0x34);
    machine.Bus.WriteZ80Byte(0x0002, 0xED); // NEG
    machine.Bus.WriteZ80Byte(0x0003, 0x44);
    machine.Bus.WriteZ80Byte(0x0004, 0x3E); // LD A,$80
    machine.Bus.WriteZ80Byte(0x0005, 0x80);
    machine.Bus.WriteZ80Byte(0x0006, 0xED); // NEG alias
    machine.Bus.WriteZ80Byte(0x0007, 0x4C);
    machine.Bus.WriteZ80Byte(0x0008, 0x3E); // LD A,$00
    machine.Bus.WriteZ80Byte(0x0009, 0x00);
    machine.Bus.WriteZ80Byte(0x000A, 0xED); // NEG alias
    machine.Bus.WriteZ80Byte(0x000B, 0x54);

    machine.Z80.SetLines(resetAsserted: false, busRequested: false);
    machine.Z80.StepInstruction(machine.Bus.ReadZ80Byte, machine.Bus.WriteZ80Byte);
    machine.Z80.StepInstruction(machine.Bus.ReadZ80Byte, machine.Bus.WriteZ80Byte);
    AssertEqual((byte)0xCC, machine.Z80.A);
    AssertEqual((byte)0x93, machine.Z80.F);

    machine.Z80.StepInstruction(machine.Bus.ReadZ80Byte, machine.Bus.WriteZ80Byte);
    machine.Z80.StepInstruction(machine.Bus.ReadZ80Byte, machine.Bus.WriteZ80Byte);
    AssertEqual((byte)0x80, machine.Z80.A);
    AssertEqual((byte)0x87, machine.Z80.F);

    machine.Z80.StepInstruction(machine.Bus.ReadZ80Byte, machine.Bus.WriteZ80Byte);
    machine.Z80.StepInstruction(machine.Bus.ReadZ80Byte, machine.Bus.WriteZ80Byte);
    AssertEqual((byte)0x00, machine.Z80.A);
    AssertEqual((byte)0x42, machine.Z80.F);
}

void Z80ControlWordWritesUseEvenByte()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));

    machine.Bus.WriteWord(0x00A1_1200, 0x0100);
    AssertTrue(!machine.Bus.Z80ResetAsserted, "word write #$0100 should deassert Z80 reset");

    machine.Bus.WriteWord(0x00A1_1200, 0x0000);
    AssertTrue(machine.Bus.Z80ResetAsserted, "word write #$0000 should assert Z80 reset");

    machine.Bus.WriteWord(0x00A1_1200, 0x0100);
    AssertTrue(!machine.Bus.Z80ResetAsserted, "word write #$0100 should deassert Z80 reset again");

    machine.Bus.WriteWord(0x00A1_1100, 0x0100);
    AssertTrue(machine.Bus.Z80BusRequested, "word write #$0100 should request the Z80 bus");
    AssertEqual((byte)0x01, (byte)(machine.Bus.ReadByte(0x00A1_1100) & 0x01));
    machine.Bus.CurrentMasterCycle += 128;
    byte grantedStatus = machine.Bus.ReadByte(0x00A1_1100);
    AssertEqual((byte)0x00, (byte)(grantedStatus & 0x01));
    AssertTrue(grantedStatus != 0, "unused bits should keep byte-sized bus grant polls from reading as zero");

    machine.Bus.WriteWord(0x00A1_1200, 0x0000);
    AssertTrue(machine.Bus.Z80ResetAsserted, "word write #$0000 should assert Z80 reset");
    AssertEqual((byte)0x01, (byte)(machine.Bus.ReadByte(0x00A1_1100) & 0x01));

    machine.Bus.WriteWord(0x00A1_1100, 0x0000);
    AssertTrue(!machine.Bus.Z80BusRequested, "word write #$0000 should release the Z80 bus");
    AssertEqual((byte)0x01, (byte)(machine.Bus.ReadByte(0x00A1_1100) & 0x01));
}

void Z80BusGrantIsDelayedAfterRequest()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Bus.WriteWord(0x00A1_1200, 0x0100);

    machine.Bus.CurrentMasterCycle = 1_000;
    machine.Bus.WriteWord(0x00A1_1100, 0x0100);
    AssertTrue(machine.Bus.Z80BusRequested, "request line should assert immediately");
    AssertTrue(!machine.Bus.Z80BusGranted, "bus should not be granted before the grant delay elapses");

    machine.Bus.CurrentMasterCycle = 1_063;
    AssertTrue(!machine.Bus.Z80BusGranted, "grant should still be pending one master cycle before the delay");
    machine.Bus.CurrentMasterCycle = 1_064;
    AssertTrue(machine.Bus.Z80BusGranted, "bus should be granted once the delay elapses");

    machine.Bus.WriteWord(0x00A1_1100, 0x0000);
    AssertTrue(!machine.Bus.Z80BusGranted, "releasing the request should release the grant");
}

void Z80RunsDuringShortBusReleaseWindows()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x33FC); // MOVE.W #$0100,$A11200: deassert Z80 reset.
    EmitWord(rom, ref pc, 0x0100);
    EmitLong(rom, ref pc, 0x00A1_1200);
    int releaseLoop = pc;
    EmitWord(rom, ref pc, 0x33FC); // MOVE.W #$0000,$A11100: briefly release the Z80 bus.
    EmitWord(rom, ref pc, 0x0000);
    EmitLong(rom, ref pc, 0x00A1_1100);
    for (int i = 0; i < 32; i++)
    {
        EmitWord(rom, ref pc, 0x4E71);
    }
    EmitWord(rom, ref pc, 0x33FC); // MOVE.W #$0100,$A11100: take the bus back.
    EmitWord(rom, ref pc, 0x0100);
    EmitLong(rom, ref pc, 0x00A1_1100);
    EmitWord(rom, ref pc, 0x0839); // BTST #7,$A01FFD
    EmitWord(rom, ref pc, 0x0007);
    EmitLong(rom, ref pc, 0x00A0_1FFD);
    int displacement = releaseLoop - (pc + 2);
    EmitWord(rom, ref pc, (ushort)(0x6600 | ((byte)(sbyte)displacement))); // BNE.S releaseLoop
    EmitWord(rom, ref pc, 0x7001); // MOVEQ #1,D0
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.Bus.WriteZ80Byte(0x0000, 0x21); // LD HL,$1FFD
    machine.Bus.WriteZ80Byte(0x0001, 0xFD);
    machine.Bus.WriteZ80Byte(0x0002, 0x1F);
    machine.Bus.WriteZ80Byte(0x0003, 0xAF); // XOR A
    machine.Bus.WriteZ80Byte(0x0004, 0x77); // LD (HL),A
    machine.Bus.WriteZ80Byte(0x0005, 0x76); // HALT
    machine.Bus.WriteZ80Byte(0x1FFD, 0x80);

    machine.RunFrame(20_000);

    AssertEqual(1u, machine.MainCpu.D[0]);
    AssertEqual((byte)0x00, machine.Bus.ReadZ80Byte(0x1FFD));
    AssertTrue(machine.Z80.Halted, "Z80 should run and halt during the brief bus release");
}

void Z80AudioTimestampsRemainMonotonicAcrossFrames()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteWord(rom, 0x200, 0x4E71); // NOP
    WriteWord(rom, 0x202, 0x60FC); // BRA -4

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    byte[] z80Program =
    [
        0x3E, 0x2B,       // LD A,$2B
        0x32, 0x00, 0x40, // LD ($4000),A
        0x3E, 0x80,       // LD A,$80
        0x32, 0x01, 0x40, // LD ($4001),A ; enable DAC
        0x3E, 0x2A,       // LD A,$2A
        0x32, 0x00, 0x40, // LD ($4000),A
        0x3E, 0x20,       // LD A,$20
        0x32, 0x01, 0x40, // LD ($4001),A
        0x3E, 0xE0,       // LD A,$E0
        0x32, 0x01, 0x40, // LD ($4001),A
        0x18, 0xEF        // JR back to the DAC sample loop
    ];

    for (int i = 0; i < z80Program.Length; i++)
    {
        machine.Bus.WriteZ80Byte((ushort)i, z80Program[i]);
    }

    long lastDacCycle = -1;
    int dacWrites = 0;
    int backwardTimestamps = 0;
    machine.Bus.AudioObserver = access =>
    {
        if (access.Chip != AudioChip.Ym2612 || access.Kind != AudioAccessKind.Data || access.Port != 0 || access.Register != 0x2A)
        {
            return;
        }

        if (lastDacCycle > access.MasterCycle)
        {
            backwardTimestamps++;
        }

        lastDacCycle = access.MasterCycle;
        dacWrites++;
    };

    machine.Bus.WriteWord(0x00A1_1200, 0x0100); // release Z80 reset
    for (int frame = 0; frame < 20; frame++)
    {
        machine.RunFrameCycles(200_000);
    }

    machine.Bus.AudioObserver = null;
    AssertTrue(dacWrites > 100, "test Z80 program should continuously stream DAC samples");
    AssertEqual(0, backwardTimestamps);
}

void Z80ReceivesVBlankInterruptPulse()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteWord(rom, 0x200, 0x4E71); // NOP
    WriteWord(rom, 0x202, 0x60FC); // BRA -4

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.Bus.WriteZ80Byte(0x0000, 0xED); // IM 1
    machine.Bus.WriteZ80Byte(0x0001, 0x56);
    machine.Bus.WriteZ80Byte(0x0002, 0xFB); // EI
    machine.Bus.WriteZ80Byte(0x0003, 0x76); // HALT
    machine.Bus.WriteZ80Byte(0x0038, 0x3A); // LD A,($0070)
    machine.Bus.WriteZ80Byte(0x0039, 0x70);
    machine.Bus.WriteZ80Byte(0x003A, 0x00);
    machine.Bus.WriteZ80Byte(0x003B, 0x3C); // INC A
    machine.Bus.WriteZ80Byte(0x003C, 0x32); // LD ($0070),A
    machine.Bus.WriteZ80Byte(0x003D, 0x70);
    machine.Bus.WriteZ80Byte(0x003E, 0x00);
    machine.Bus.WriteZ80Byte(0x003F, 0xF3); // DI
    machine.Bus.WriteZ80Byte(0x0040, 0xC9); // RET
    machine.Vdp.WriteControlPort(0x8124); // display + VBlank interrupt enable
    machine.Bus.WriteWord(0x00A1_1200, 0x0100); // release Z80 reset

    machine.RunFrameCycles(200_000);

    AssertEqual((byte)1, machine.Bus.Z80Ram[0x70]);
}

void Z80VBlankInterruptIgnores68kEnable()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteWord(rom, 0x200, 0x4E71); // NOP
    WriteWord(rom, 0x202, 0x60FC); // BRA -4

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.Bus.WriteZ80Byte(0x0000, 0xED); // IM 1
    machine.Bus.WriteZ80Byte(0x0001, 0x56);
    machine.Bus.WriteZ80Byte(0x0002, 0xFB); // EI
    machine.Bus.WriteZ80Byte(0x0003, 0x76); // HALT
    machine.Bus.WriteZ80Byte(0x0038, 0x3A); // LD A,($0070)
    machine.Bus.WriteZ80Byte(0x0039, 0x70);
    machine.Bus.WriteZ80Byte(0x003A, 0x00);
    machine.Bus.WriteZ80Byte(0x003B, 0x3C); // INC A
    machine.Bus.WriteZ80Byte(0x003C, 0x32); // LD ($0070),A
    machine.Bus.WriteZ80Byte(0x003D, 0x70);
    machine.Bus.WriteZ80Byte(0x003E, 0x00);
    machine.Bus.WriteZ80Byte(0x003F, 0xF3); // DI
    machine.Bus.WriteZ80Byte(0x0040, 0xC9); // RET
    machine.Vdp.WriteControlPort(0x8104); // 68k VBlank interrupt disabled.
    machine.Bus.WriteWord(0x00A1_1200, 0x0100); // release Z80 reset

    machine.RunFrameCycles(200_000);

    AssertEqual((byte)1, machine.Bus.Z80Ram[0x70]);
}

void Sonic1StartupStreamsYmDacSample()
{
    string path = Path.Combine("TestRoms", "Sonic.md");
    if (!File.Exists(path))
    {
        return;
    }

    MegaDrive machine = new(CartridgeImage.FromFile(path));
    machine.Reset();
    int dacWrites = 0;
    int dacEnableWrites = 0;
    machine.Bus.AudioObserver = access =>
    {
        if (access.Chip != AudioChip.Ym2612 || access.Kind != AudioAccessKind.Data || access.Port != 0)
        {
            return;
        }

        if (access.Register == 0x2A)
        {
            dacWrites++;
        }
        else if (access.Register == 0x2B)
        {
            dacEnableWrites++;
        }
    };

    for (int frame = 0; frame < 400; frame++)
    {
        machine.RunFrameCycles(300_000);
        _ = machine.RenderFrameStereoAudioSamples();
    }

    machine.Bus.AudioObserver = null;
    AssertTrue(dacEnableWrites > 0, "Sonic 1 startup should enable the YM DAC");
    AssertTrue(dacWrites > 10_000, "Sonic 1 startup should stream the SEGA voice through YM DAC register $2A");
}

void Sonic1TitleDrivesYmAndPsgMusic()
{
    string path = Path.Combine("TestRoms", "Sonic.md");
    if (!File.Exists(path))
    {
        return;
    }

    MegaDrive machine = new(CartridgeImage.FromFile(path));
    machine.Reset();
    int keyOnWrites = 0;
    int psgWrites = 0;
    long audioEnergy = 0;
    machine.Bus.AudioObserver = access =>
    {
        if (access.Chip == AudioChip.Ym2612 && access.Kind == AudioAccessKind.Data && access.Port == 0 && access.Register == 0x28)
        {
            keyOnWrites++;
        }
        else if (access.Chip == AudioChip.Psg)
        {
            psgWrites++;
        }
    };

    for (int frame = 0; frame < 900; frame++)
    {
        machine.RunFrameCycles(300_000);
        short[] samples = machine.RenderFrameStereoAudioSamples();
        if (frame >= 500)
        {
            for (int i = 0; i < samples.Length; i += 64)
            {
                audioEnergy += Math.Abs(samples[i]);
            }
        }
    }

    machine.Bus.AudioObserver = null;
    AssertTrue(keyOnWrites > 100, "Sonic 1 title path should actively key YM channels for music");
    AssertTrue(psgWrites > 100, "Sonic 1 title path should actively drive PSG channels for music/effects");
    AssertTrue(audioEnergy > 100_000, "Sonic 1 title path should produce sustained mixed audio");
}

void WorkRamMirroring()
{
    MegaDrive machine = new(CartridgeImage.FromBytes(CreateRom()));
    machine.Bus.WriteByte(0xE00000, 0x5A);
    AssertEqual((byte)0x5A, machine.Bus.ReadByte(0xFF0000));
}

void CartridgeSaveRam()
{
    byte[] rom = CreateRom();
    DeclareSaveRam(rom);
    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    AssertEqual((byte)0x00, machine.Bus.ReadByte(0x200123));
    machine.Bus.WriteByte(0xA130F1, 0x01);
    machine.Bus.WriteByte(0x200123, 0xA5);
    AssertEqual((byte)0xA5, machine.Bus.ReadByte(0x200123));

    byte[] noRamRom = CreateRom();
    noRamRom[0x123] = 0x5A;
    MegaDrive noRamMachine = new(CartridgeImage.FromBytes(noRamRom));
    AssertEqual((byte)0x5A, noRamMachine.Bus.ReadByte(0x200123));
    noRamMachine.Bus.WriteByte(0x200123, 0xA5);
    AssertEqual((byte)0xA5, noRamMachine.Bus.ReadByte(0x200123));
}

void CartridgeSaveRamByteLanes()
{
    byte[] oddRom = CreateRom();
    DeclareSaveRam(oddRom, 0x0020_0001, 0x0020_3FFF, 0x20);
    MegaDrive oddMachine = new(CartridgeImage.FromBytes(oddRom));
    oddMachine.Bus.WriteByte(0xA130F1, 0x01);
    oddMachine.Bus.WriteByte(0x200001, 0x5A);
    oddMachine.Bus.WriteByte(0x200002, 0xC3);
    oddMachine.Bus.WriteByte(0x200003, 0x6B);
    AssertEqual((byte)0x5A, oddMachine.Bus.ReadByte(0x200001));
    AssertEqual((byte)0xFF, oddMachine.Bus.ReadByte(0x200002));
    AssertEqual((byte)0x6B, oddMachine.Bus.ReadByte(0x200003));
    AssertEqual((byte)0x5A, oddMachine.Bus.Cartridge.SaveRam[0]);
    AssertEqual((byte)0x6B, oddMachine.Bus.Cartridge.SaveRam[1]);

    byte[] evenRom = CreateRom();
    DeclareSaveRam(evenRom, 0x0020_0000, 0x0020_3FFE, 0x40);
    MegaDrive evenMachine = new(CartridgeImage.FromBytes(evenRom));
    evenMachine.Bus.WriteByte(0xA130F1, 0x01);
    evenMachine.Bus.WriteByte(0x200000, 0x11);
    evenMachine.Bus.WriteByte(0x200001, 0x22);
    evenMachine.Bus.WriteByte(0x200002, 0x33);
    AssertEqual((byte)0x11, evenMachine.Bus.ReadByte(0x200000));
    AssertEqual((byte)0xFF, evenMachine.Bus.ReadByte(0x200001));
    AssertEqual((byte)0x33, evenMachine.Bus.ReadByte(0x200002));
    AssertEqual((byte)0x11, evenMachine.Bus.Cartridge.SaveRam[0]);
    AssertEqual((byte)0x33, evenMachine.Bus.Cartridge.SaveRam[1]);

    byte[] oddSingleRom = CreateRom();
    DeclareSaveRam(oddSingleRom, 0x0020_0001, 0x0020_0001, 0x40);
    MegaDrive oddSingleMachine = new(CartridgeImage.FromBytes(oddSingleRom));
    AssertEqual((byte)0x00, oddSingleMachine.Bus.ReadByte(0x200001));
    oddSingleMachine.Bus.WriteByte(0x200001, 0x7E);
    AssertEqual((byte)0x7E, oddSingleMachine.Bus.ReadByte(0x200001));
    AssertEqual((byte)0x7E, oddSingleMachine.Bus.Cartridge.SaveRam[0]);
}

void CartridgeSerialEeprom()
{
    byte[] rom = CreateRom();
    DeclareEeprom(rom);
    MegaDrive machine = new(CartridgeImage.FromBytes(rom));

    EepromPins segaPins = new(0x200001, 0, 0x200001, 0, 0x200001, 1);
    EepromStart(machine, segaPins);
    EepromSendByte(machine, segaPins, 0x10); // Address $08, write.
    AssertEqual((byte)0xFE, (byte)(machine.Bus.ReadByte(0x200001) & 0x01 | 0xFE));
    EepromAckClock(machine, segaPins);
    EepromSendByte(machine, segaPins, 0xA5);
    EepromAckClock(machine, segaPins);
    EepromStop(machine, segaPins);

    EepromStart(machine, segaPins);
    EepromSendByte(machine, segaPins, 0x11); // Address $08, read.
    EepromAckClock(machine, segaPins);
    byte readBack = EepromReadByte(machine, segaPins, ack: false);
    EepromStop(machine, segaPins);

    AssertEqual((byte)0xA5, readBack);
    AssertEqual((byte)0xA5, machine.Bus.Cartridge.SaveRam[0x08]);

    byte[] nbaJamRom = CreateRom();
    WriteAscii(nbaJamRom, 0x180, "T-081326");
    MegaDrive nbaJamMachine = new(CartridgeImage.FromBytes(nbaJamRom));
    EepromPins nbaJamPins = new(0x200001, 0, 0x200001, 1, 0x200001, 1);
    EepromWriteBytes(nbaJamMachine, nbaJamPins, command: 0xA0, addressBytes: [0x5C], data: [0x3C]);
    byte mode2 = EepromReadAt(nbaJamMachine, nbaJamPins, writeCommand: 0xA0, readCommand: 0xA1, addressBytes: [0x5C]);
    AssertEqual((byte)0x3C, mode2);
    AssertEqual((byte)0x3C, nbaJamMachine.Bus.Cartridge.SaveRam[0x5C]);

    byte[] quarterbackRom = CreateRom();
    WriteAscii(quarterbackRom, 0x180, "T-081276");
    MegaDrive quarterbackMachine = new(CartridgeImage.FromBytes(quarterbackRom));
    EepromPins quarterbackPins = new(0x200001, 0, 0x200001, 0, 0x200000, 0, WordAccess: true);
    EepromWriteBytes(quarterbackMachine, quarterbackPins, command: 0xA0, addressBytes: [0x71], data: [0x96]);
    byte wordAccessMode2 = EepromReadAt(quarterbackMachine, quarterbackPins, writeCommand: 0xA0, readCommand: 0xA1, addressBytes: [0x71]);
    AssertEqual((byte)0x96, wordAccessMode2);
    AssertEqual((byte)0x96, quarterbackMachine.Bus.Cartridge.SaveRam[0x71]);

    byte[] codemastersRom = CreateRom();
    WriteAscii(codemastersRom, 0x150, "BRIAN LARA CRICKET 96");
    MegaDrive codemastersMachine = new(CartridgeImage.FromBytes(codemastersRom));
    EepromPins codemastersPins = new(0x300000, 0, 0x380001, 7, 0x300000, 1);
    EepromWriteBytes(codemastersMachine, codemastersPins, command: 0xA0, addressBytes: [0x12, 0x34], data: [0xC7]);
    byte mode3 = EepromReadAt(codemastersMachine, codemastersPins, writeCommand: 0xA0, readCommand: 0xA1, addressBytes: [0x12, 0x34]);
    AssertEqual((byte)0xC7, mode3);
    AssertEqual((byte)0xC7, codemastersMachine.Bus.Cartridge.SaveRam[0x1234]);
}

void InputMoviePreservesInitialSaveRam()
{
    byte[] rom = CreateRom();
    DeclareSaveRam(rom);
    CartridgeImage source = CartridgeImage.FromBytes(rom);
    byte[] saveRam = new byte[64 * 1024];
    saveRam[0x0123] = 0x5A;
    saveRam[0x7FFF] = 0xC3;
    source.RestoreSaveRam(saveRam);

    InputMovie movie = InputMovie.Create("synthetic.md", source);
    CartridgeImage restored = CartridgeImage.FromBytes(rom);
    movie.RestoreInitialSaveRam(restored);

    AssertEqual((byte)0x5A, restored.SaveRam[0x0123]);
    AssertEqual((byte)0xC3, restored.SaveRam[0x7FFF]);
}

void CartridgeBankSwitching()
{
    byte[] rom = new byte[0x10_0000];
    rom[0x000000] = 0x11;
    rom[0x080000] = 0x22;
    CartridgeImage image = CartridgeImage.FromBytes(rom);
    MegaDrive machine = new(image);

    AssertEqual((byte)0x22, machine.Bus.ReadByte(0x080000));
    machine.Bus.WriteByte(0xA13000, 0x00);
    AssertEqual((byte)0x11, machine.Bus.ReadByte(0x080000));
    machine.Bus.WriteByte(0xA13000, 0x01);
    AssertEqual((byte)0x22, machine.Bus.ReadByte(0x080000));

    byte[] ssf2Rom = new byte[0x50_0000];
    ssf2Rom[0x080000] = 0x33;
    ssf2Rom[0x400000] = 0x44;
    MegaDrive ssf2Machine = new(CartridgeImage.FromBytes(ssf2Rom));

    AssertEqual((byte)0x33, ssf2Machine.Bus.ReadByte(0x080000));
    ssf2Machine.Bus.WriteByte(0xA130F3, 0x08);
    AssertEqual((byte)0x44, ssf2Machine.Bus.ReadByte(0x080000));
}

void JCartControllerPorts()
{
    byte[] rom = CreateRom();
    WriteAscii(rom, 0x150, "MICRO MACHINES II");
    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Bus.Controller3.Pressed = GenesisButton.B;
    machine.Bus.Controller4.Pressed = GenesisButton.C;

    ushort high = machine.Bus.ReadWord(0x380000);
    AssertTrue((high & 0x0010) == 0, "J-Cart player 3 B should be visible in the low byte");
    AssertTrue((high & 0x2000) == 0, "J-Cart player 4 C should be visible in the high byte");
    AssertTrue((high & 0x4000) == 0, "J-Cart D14 should stay low");

    machine.Bus.Controller3.Pressed = GenesisButton.A | GenesisButton.Start;
    machine.Bus.Controller4.Pressed = GenesisButton.A;
    machine.Bus.WriteByte(0x380000, 0x00);
    ushort low = machine.Bus.ReadWord(0x380000);
    AssertTrue((low & 0x000C) == 0, "J-Cart player 3 should expose the low-TH controller signature");
    AssertTrue((low & 0x0010) == 0, "J-Cart player 3 A should be visible in the low-TH phase");
    AssertTrue((low & 0x0020) == 0, "J-Cart player 3 Start should be visible in the low-TH phase");
    AssertTrue((low & 0x1000) == 0, "J-Cart player 4 A should be visible in the high byte low-TH phase");

    byte[] eepromRom = CreateRom();
    WriteAscii(eepromRom, 0x180, "T-120096");
    MegaDrive eepromMachine = new(CartridgeImage.FromBytes(eepromRom));
    AssertTrue(eepromMachine.Bus.Cartridge.Diagnostics.HasJCart, "Micro Machines 2 should enable J-Cart diagnostics");
    byte lowByte = eepromMachine.Bus.ReadByte(0x380001);
    AssertTrue((lowByte & 0x80) != 0, "J-Cart EEPROM SDA-out should be preserved on D7");
}

void SvpCartridgeMemoryMap()
{
    byte[] rom = CreateRom();
    WriteAscii(rom, 0x150, "VIRTUA RACING");
    MegaDrive machine = new(CartridgeImage.FromBytes(rom));

    AssertTrue(machine.Bus.Cartridge.Diagnostics.HasSvp, "Virtua Racing should enable SVP diagnostics");
    AssertTrue(machine.Bus.Svp is not null, "SVP device should be attached to Virtua Racing cartridges");

    machine.Bus.WriteWord(0x0030_0000, 0x1234);
    AssertEqual(0x1234, machine.Bus.ReadWord(0x0030_0000));
    AssertEqual(0x12, machine.Bus.ReadByte(0x0030_0000));
    AssertEqual(0x34, machine.Bus.ReadByte(0x0030_0001));

    machine.Bus.WriteByte(0x0030_0001, 0x56);
    AssertEqual(0x1256, machine.Bus.ReadWord(0x0030_0000));
    AssertEqual(0x1256, machine.Bus.ReadWord(0x0039_0000));

    machine.Bus.WriteWord(0x0030_0100, 0x5678);
    AssertEqual(0x5678, machine.Bus.ReadWord(0x0039_0004));

    machine.Bus.WriteWord(0x0030_0102, 0x9ABC);
    AssertEqual(0x9ABC, machine.Bus.ReadWord(0x003A_0006));

    machine.Bus.WriteWord(0x00A1_5000, 0xCAFE);
    AssertEqual(0xCAFE, machine.Bus.ReadWord(0x00A1_5000));
    AssertTrue((machine.Bus.ReadWord(0x00A1_5004) & 0x0002) != 0, "SVP status should report a pending host command");
}

void SvpPointerWritesIgnoreModuloLength()
{
    byte[] incrementRom = CreateRom();
    int offset = 0x800;
    WriteWord(incrementRom, offset, 0x0840); offset += 2; // ldi st,#$0002. Writes still post-increment the raw pointer.
    WriteWord(incrementRom, offset, 0x0002); offset += 2;
    WriteWord(incrementRom, offset, 0x1803); offset += 2; // ldi r0,#$03.
    WriteWord(incrementRom, offset, 0x0C0C); offset += 2; // ldi (r0+),#$AAAA.
    WriteWord(incrementRom, offset, 0xAAAA); offset += 2;
    WriteWord(incrementRom, offset, 0x0C0C); offset += 2; // ldi (r0+),#$BBBB; raw pointer advances to 4.
    WriteWord(incrementRom, offset, 0xBBBB);

    SvpDevice increment = new(incrementRom);
    increment.Run(8);
    SvpDevice.SvpState incrementState = increment.CaptureState();

    AssertEqual(0x0000, incrementState.Ram[0]);
    AssertEqual(0xAAAA, incrementState.Ram[3]);
    AssertEqual(0xBBBB, incrementState.Ram[4]);
    AssertEqual((byte)5, incrementState.Pointers[0]);

    byte[] decrementRom = CreateRom();
    offset = 0x800;
    WriteWord(decrementRom, offset, 0x0840); offset += 2; // ldi st,#$0002.
    WriteWord(decrementRom, offset, 0x0002); offset += 2;
    WriteWord(decrementRom, offset, 0x1800); offset += 2; // ldi r0,#$00.
    WriteWord(decrementRom, offset, 0x0C08); offset += 2; // ldi (r0-),#$CCCC.
    WriteWord(decrementRom, offset, 0xCCCC); offset += 2;
    WriteWord(decrementRom, offset, 0x0C08); offset += 2; // ldi (r0-),#$DDDD; raw pointer underflows to $FF.
    WriteWord(decrementRom, offset, 0xDDDD);

    SvpDevice decrement = new(decrementRom);
    decrement.Run(8);
    SvpDevice.SvpState decrementState = decrement.CaptureState();

    AssertEqual(0xCCCC, decrementState.Ram[0]);
    AssertEqual(0x0000, decrementState.Ram[3]);
    AssertEqual(0xDDDD, decrementState.Ram[0xFF]);
    AssertEqual((byte)0xFE, decrementState.Pointers[0]);
}

void SvpImmediateOpsUseReferenceTiming()
{
    byte[] rom = CreateRom();
    int offset = 0x800;
    WriteWord(rom, offset, 0x0830); offset += 2; // ldi a,#$1234.
    WriteWord(rom, offset, 0x1234); offset += 2;
    WriteWord(rom, offset, 0x0000); // nop.

    SvpDevice svp = new(rom);
    svp.Run(2);
    SvpDevice.SvpState state = svp.CaptureState();

    AssertEqual(0x1234_0000u, state.Gr[3]);
    AssertEqual(0x0403, state.Pc);
}

void SvpOptionalMameTimingChargesImmediateCycles()
{
    byte[] rom = CreateRom();
    int offset = 0x800;
    WriteWord(rom, offset, 0x0830); offset += 2; // ldi a,#$1234.
    WriteWord(rom, offset, 0x1234); offset += 2;
    WriteWord(rom, offset, 0x0000); // nop.

    SvpDevice svp = new(rom)
    {
        UseMameCycleTiming = true
    };
    svp.Run(2);
    SvpDevice.SvpState state = svp.CaptureState();

    AssertEqual(0x1234_0000u, state.Gr[3]);
    AssertEqual(0x0402, state.Pc);
}

void SvpMldClearsStatusFlagsWithoutSettingZ()
{
    byte[] rom = CreateRom();
    int offset = 0x800;
    WriteWord(rom, offset, 0x0840); offset += 2; // ldi st,#$FFFF.
    WriteWord(rom, offset, 0xFFFF); offset += 2;
    WriteWord(rom, offset, 0xB600); // mld (r4),(r0),b.

    SvpDevice svp = new(rom);
    svp.Run(2);
    SvpDevice.SvpState state = svp.CaptureState();

    AssertEqual(0x0FFFu << 16, state.Gr[4]);
}

void SvpAlReadPreservesPendingPmacExceptDummyAssign()
{
    byte[] rom = CreateRom();
    int offset = 0x800;
    WriteWord(rom, offset, 0x08E0); offset += 2; // ldi pmc,#$0005: address.
    WriteWord(rom, offset, 0x0005); offset += 2;
    WriteWord(rom, offset, 0x08E0); offset += 2; // ldi pmc,#$0818: DRAM linear, increment by one.
    WriteWord(rom, offset, 0x0818); offset += 2;
    WriteWord(rom, offset, 0x001F); offset += 2; // ld x,al: regular AL read should not discard pending PMAC.
    WriteWord(rom, offset, 0x00C0); offset += 2; // ld pm4,gr0: blind PMAC assignment for PM4 writes.
    WriteWord(rom, offset, 0x0830); offset += 2; // ldi a,#$CAFE.
    WriteWord(rom, offset, 0xCAFE); offset += 2;
    WriteWord(rom, offset, 0x00C3); // ld pm4,a: write A high word to DRAM.

    SvpDevice svp = new(rom);
    svp.Run(8);
    SvpDevice.SvpState state = svp.CaptureState();

    AssertEqual(0xCAFE, state.Dram[5]);
}

void SvpPmTraceCapturesDramWrites()
{
    byte[] rom = CreateRom();
    int offset = 0x800;
    WriteWord(rom, offset, 0x08E0); offset += 2; // ldi pmc,#$0005: address.
    WriteWord(rom, offset, 0x0005); offset += 2;
    WriteWord(rom, offset, 0x08E0); offset += 2; // ldi pmc,#$0818: DRAM linear, increment by one.
    WriteWord(rom, offset, 0x0818); offset += 2;
    WriteWord(rom, offset, 0x00C0); offset += 2; // ld pm4,gr0: blind PMAC assignment for PM4 writes.
    WriteWord(rom, offset, 0x0830); offset += 2; // ldi a,#$ABCD.
    WriteWord(rom, offset, 0xABCD); offset += 2;
    WriteWord(rom, offset, 0x00C3); // ld pm4,a: write A high word to DRAM.

    List<SvpDevice.SvpPmIoTrace> traces = [];
    SvpDevice svp = new(rom)
    {
        PmIoObserver = traces.Add
    };

    svp.Run(16);
    SvpDevice.SvpState state = svp.CaptureState();

    AssertEqual(0xABCD, state.Dram[5]);
    AssertTrue(traces.Any(trace => trace.Kind == "PmacSet" && trace.Register == 4 && trace.Write && trace.PmacAfter == 0x0818_0005), "trace should include PM4 PMAC assignment");
    SvpDevice.SvpPmIoTrace write = traces.First(trace => trace.Kind == "DramLinear" && trace.Register == 4 && trace.Write);
    AssertEqual(0x0005, write.AddressBefore);
    AssertEqual(0x0006, write.AddressAfter);
    AssertEqual(0xABCD, write.Data);
    AssertEqual(0x0000, write.PreviousValue);
    AssertEqual(0xABCD, write.StoredValue);
}

void SvpPointerTraceCapturesRamOperands()
{
    byte[] rom = CreateRom();
    int offset = 0x800;
    WriteWord(rom, offset, 0x1801); offset += 2; // ldi r0,#$01.
    WriteWord(rom, offset, 0x0C0C); offset += 2; // ldi (r0++),#$1234.
    WriteWord(rom, offset, 0x1234); offset += 2;
    WriteWord(rom, offset, 0x1801); offset += 2; // ldi r0,#$01.
    WriteWord(rom, offset, 0x0230); // ld a,(r0).

    List<SvpDevice.SvpPointerTrace> traces = [];
    SvpDevice svp = new(rom)
    {
        PointerObserver = traces.Add
    };

    svp.Run(12);

    SvpDevice.SvpPointerTrace write = traces.First(trace => trace.Operation == "Ptr1WriteIncrement");
    AssertEqual(0x0802, write.Pc);
    AssertEqual((byte)0x01, write.PointerBefore);
    AssertEqual((byte)0x02, write.PointerAfter);
    AssertEqual(0x001, write.RamAddress);
    AssertEqual(0x1234, write.Value);

    SvpDevice.SvpPointerTrace read = traces.First(trace => trace.Operation == "Ptr1Read");
    AssertEqual(0x0808, read.Pc);
    AssertEqual((byte)0x01, read.PointerBefore);
    AssertEqual((byte)0x01, read.PointerAfter);
    AssertEqual(0x001, read.RamAddress);
    AssertEqual(0x1234, read.Value);
}

void SaveStateRoundTrip()
{
    byte[] rom = CreateRom();
    DeclareSaveRam(rom);
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteWord(rom, 0x200, 0x7001); // MOVEQ #1,D0
    WriteWord(rom, 0x202, 0x4E71); // NOP
    WriteWord(rom, 0x204, 0x60FE); // BRA *

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.StepInstruction();
    machine.Bus.WriteByte(0xA130F1, 0x01);
    machine.Bus.WriteByte(0x200000, 0x44);
    string path = Path.Combine(Path.GetTempPath(), $"mdsharp-{Guid.NewGuid():N}.mdss");

    SaveStateSerializer.Save(machine, path);
    machine.StepInstruction();
    machine.Bus.WriteByte(0x200000, 0x55);
    SaveStateSerializer.Load(machine, path);

    AssertEqual(1u, machine.MainCpu.D[0]);
    AssertEqual((byte)0x44, machine.Bus.ReadByte(0x200000));
    File.Delete(path);

    byte[] fallbackRom = CreateRom();
    fallbackRom[0x100] = 0x6A;
    MegaDrive fallback = new(CartridgeImage.FromBytes(fallbackRom));
    fallback.Bus.WriteByte(0x200100, 0x77);
    string fallbackPath = Path.Combine(Path.GetTempPath(), $"mdsharp-{Guid.NewGuid():N}.mdss");

    SaveStateSerializer.Save(fallback, fallbackPath);
    fallback.Bus.WriteByte(0x200100, 0x33);
    SaveStateSerializer.Load(fallback, fallbackPath);

    AssertEqual((byte)0x77, fallback.Bus.ReadByte(0x200100));
    File.Delete(fallbackPath);

    byte[] svpRom = CreateRom();
    WriteAscii(svpRom, 0x150, "VIRTUA RACING");
    MegaDrive svpMachine = new(CartridgeImage.FromBytes(svpRom));
    svpMachine.Bus.WriteWord(0x0030_0000, 0xBEEF);
    svpMachine.Bus.WriteWord(0x00A1_5000, 0xCAFE);
    string svpPath = Path.Combine(Path.GetTempPath(), $"mdsharp-{Guid.NewGuid():N}.mdss");

    SaveStateSerializer.Save(svpMachine, svpPath);
    svpMachine.Bus.WriteWord(0x0030_0000, 0x1234);
    svpMachine.Bus.WriteWord(0x00A1_5000, 0x4444);
    SaveStateSerializer.Load(svpMachine, svpPath);

    AssertEqual(0xBEEF, svpMachine.Bus.ReadWord(0x0030_0000));
    AssertEqual(0xCAFE, svpMachine.Bus.ReadWord(0x00A1_5000));
    File.Delete(svpPath);

    byte[] x32Rom = new byte[0x400000];
    WriteAscii(x32Rom, 0x100, "SEGA 32X");
    WriteWord(x32Rom, 0x000, 0x001B); // SLEEP
    MegaDrive x32Machine = new(CartridgeImage.FromBytes(x32Rom));
    x32Machine.Reset();
    x32Machine.Bus.WriteWord(0xA1_5100, 0x0083);
    x32Machine.Bus.WriteWord(0xA1_5120, 0x1357);
    x32Machine.Bus.WriteWord(0xA1_5200, 0x2468);
    x32Machine.Bus.WriteWord(0xA1_5130, 0x0105);
    x32Machine.Bus.WriteWord(0xA1_5132, 0x0800);
    x32Machine.Bus.WriteWord(0xA1_5134, 0x0100);
    x32Machine.Bus.WriteWord(0xA1_5134, 0x0200);
    x32Machine.Bus.WriteWord(0xA1_5134, 0x0300);
    string x32Path = Path.Combine(Path.GetTempPath(), $"mdsharp-{Guid.NewGuid():N}.mdss");

    SaveStateSerializer.Save(x32Machine, x32Path);
    x32Machine.Bus.WriteWord(0xA1_5100, 0x0081);
    x32Machine.Bus.WriteWord(0xA1_5120, 0x0000);
    x32Machine.Bus.WriteWord(0xA1_5200, 0x0000);
    SaveStateSerializer.Load(x32Machine, x32Path);

    AssertEqual((ushort)0x1357, x32Machine.Bus.ReadWord(0xA1_5120));
    AssertEqual((ushort)0x2468, x32Machine.Bus.ReadWord(0xA1_5200));
    AssertTrue((x32Machine.Bus.ReadWord(0xA1_5134) & 0x8000) != 0, "32X save state should restore PWM FIFO status");
    AssertTrue(x32Machine.Bus.ThirtyTwoX is not null && !x32Machine.Bus.ThirtyTwoX.Sh2HeldInReset, "32X save state should restore adapter control state");
    File.Delete(x32Path);
}

void SyntheticGenesisStartupRom()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x1039); // MOVE.B $A10001,D0
    EmitLong(rom, ref pc, 0x00A1_0001);
    EmitWord(rom, ref pc, 0x0200); // ANDI.B #$0F,D0
    EmitWord(rom, ref pc, 0x000F);
    EmitWord(rom, ref pc, 0x670A); // BEQ.S past TMSS write
    EmitWord(rom, ref pc, 0x23FC); // MOVE.L #'SEGA',$A14000
    EmitLong(rom, ref pc, 0x5345_4741);
    EmitLong(rom, ref pc, 0x00A1_4000);
    EmitWord(rom, ref pc, 0x4DF9); // LEA $C00004,A6
    EmitLong(rom, ref pc, 0x00C0_0004);
    EmitWord(rom, ref pc, 0x3CBC); // MOVE.W #$8F02,(A6)
    EmitWord(rom, ref pc, 0x8F02);
    EmitWord(rom, ref pc, 0x2CBC); // MOVE.L #$40000000,(A6)
    EmitLong(rom, ref pc, 0x4000_0000);
    EmitWord(rom, ref pc, 0x33FC); // MOVE.W #$1234,$C00000
    EmitWord(rom, ref pc, 0x1234);
    EmitLong(rom, ref pc, 0x00C0_0000);
    EmitWord(rom, ref pc, 0x7E03); // MOVEQ #3,D7
    EmitWord(rom, ref pc, 0x51CF); // DBRA D7,*
    EmitWord(rom, ref pc, 0xFFFE);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 32; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual((byte)'S', machine.Bus.TmssRegister[0]);
    AssertEqual((byte)'E', machine.Bus.TmssRegister[1]);
    AssertEqual((byte)'G', machine.Bus.TmssRegister[2]);
    AssertEqual((byte)'A', machine.Bus.TmssRegister[3]);
    AssertEqual((byte)0x02, machine.Vdp.AutoIncrement);
    AssertEqual((byte)0x12, machine.Vdp.Vram[0]);
    AssertEqual((byte)0x34, machine.Vdp.Vram[1]);
    AssertEqual(0x0000_FFFFu, machine.MainCpu.D[7] & 0xFFFF);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after the startup program");
}

void SyntheticGenesisVBlankInterrupt()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteLong(rom, 0x078, 0x0000_0300); // Level 6 interrupt vector.

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x46FC); // MOVE #$2300,SR
    EmitWord(rom, ref pc, 0x2300);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2300
    EmitWord(rom, ref pc, 0x2300);
    EmitWord(rom, ref pc, 0x60FE); // BRA *

    pc = 0x300;
    EmitWord(rom, ref pc, 0x7C06); // MOVEQ #6,D6
    EmitWord(rom, ref pc, 0x4E73); // RTE

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.Vdp.WriteControlPort(0x8120); // Enable VBlank interrupt.

    for (int i = 0; i < 2; i++)
    {
        machine.StepInstruction();
    }

    AssertTrue(machine.MainCpu.Stopped, "CPU should stop before VBlank");
    machine.RunFrame(20_000);
    AssertEqual(6u, machine.MainCpu.D[6]);
}

void SyntheticGenesisPendingVBlankInterruptAfterUnmask()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteLong(rom, 0x078, 0x0000_0300); // Level 6 interrupt vector.

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x46FC); // MOVE #$2700,SR: mask VBlank at the scanline edge.
    EmitWord(rom, ref pc, 0x2700);
    EmitWord(rom, ref pc, 0x303C); // MOVE.W #12000,D0.
    EmitWord(rom, ref pc, 12000);
    EmitWord(rom, ref pc, 0x51C8); // DBRA D0,*: stay masked until after VBlank begins.
    EmitWord(rom, ref pc, 0xFFFE);
    EmitWord(rom, ref pc, 0x46FC); // MOVE #$2300,SR: unmask level 6 before the frame ends.
    EmitWord(rom, ref pc, 0x2300);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2300 if the pending interrupt is lost.
    EmitWord(rom, ref pc, 0x2300);
    EmitWord(rom, ref pc, 0x60FE); // BRA *

    pc = 0x300;
    EmitWord(rom, ref pc, 0x7C06); // MOVEQ #6,D6
    EmitWord(rom, ref pc, 0x4E73); // RTE

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.Vdp.WriteControlPort(0x8120); // Enable VBlank interrupt.

    machine.RunFrame(300_000);
    AssertEqual(6u, machine.MainCpu.D[6]);
}

void ExpandedCpuInstructions()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x203C); // MOVE.L #$11112222,D0
    EmitLong(rom, ref pc, 0x1111_2222);
    EmitWord(rom, ref pc, 0x223C); // MOVE.L #$33334444,D1
    EmitLong(rom, ref pc, 0x3333_4444);
    EmitWord(rom, ref pc, 0x207C); // MOVEA.L #$55556666,A0
    EmitLong(rom, ref pc, 0x5555_6666);
    EmitWord(rom, ref pc, 0x2E7C); // MOVEA.L #$00FF2000,A7
    EmitLong(rom, ref pc, 0x00FF_2000);
    EmitWord(rom, ref pc, 0x48E7); // MOVEM.L D0-D1/A0,-(A7)
    EmitWord(rom, ref pc, 0xC080);
    EmitWord(rom, ref pc, 0x4280); // CLR.L D0
    EmitWord(rom, ref pc, 0x4281); // CLR.L D1
    EmitWord(rom, ref pc, 0x4CDF); // MOVEM.L (A7)+,D2-D3/A1
    EmitWord(rom, ref pc, 0x020C);
    EmitWord(rom, ref pc, 0x7005); // MOVEQ #5,D0
    EmitWord(rom, ref pc, 0x5680); // ADDQ.L #3,D0
    EmitWord(rom, ref pc, 0x5540); // SUBQ.W #2,D0
    EmitWord(rom, ref pc, 0x7A0E); // MOVEQ #14,D5
    EmitWord(rom, ref pc, 0xD280); // ADD.L D0,D1
    EmitWord(rom, ref pc, 0x9280); // SUB.L D0,D1
    EmitWord(rom, ref pc, 0x0C40); // CMPI.W #6,D0
    EmitWord(rom, ref pc, 0x0006);
    EmitWord(rom, ref pc, 0x0800); // BTST #1,D0
    EmitWord(rom, ref pc, 0x0001);
    EmitWord(rom, ref pc, 0x247C); // MOVEA.L #$260,A2
    EmitLong(rom, ref pc, 0x0000_0260);
    EmitWord(rom, ref pc, 0x4E92); // JSR (A2)
    EmitWord(rom, ref pc, 0x4879); // PEA $00FF3000
    EmitLong(rom, ref pc, 0x00FF_3000);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    pc = 0x260;
    EmitWord(rom, ref pc, 0x7809); // MOVEQ #9,D4
    EmitWord(rom, ref pc, 0x4E75); // RTS

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 40; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0x1111_2222u, machine.MainCpu.D[2]);
    AssertEqual(0x3333_4444u, machine.MainCpu.D[3]);
    AssertEqual(0x5555_6666u, machine.MainCpu.A[1]);
    AssertEqual(6u, machine.MainCpu.D[0] & 0xFFFF);
    AssertEqual(0u, machine.MainCpu.D[1]);
    AssertEqual(9u, machine.MainCpu.D[4]);
    AssertEqual(0x00FF_1FFCu, machine.MainCpu.A[7]);
    AssertEqual(0x00FF_3000u, machine.Bus.ReadLong(0x00FF_1FFC));
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after expanded instruction program");
}

void M68kMoveByteDbfFillLoopFastForward()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x7200); // MOVEQ #0,D1
    EmitWord(rom, ref pc, 0x123C); // MOVE.B #$7F,D1
    EmitWord(rom, ref pc, 0x007F);
    EmitWord(rom, ref pc, 0x207C); // MOVEA.L #$00FF1000,A0
    EmitLong(rom, ref pc, 0x00FF_1000);
    EmitWord(rom, ref pc, 0x303C); // MOVE.W #3,D0
    EmitWord(rom, ref pc, 0x0003);
    uint loopPc = (uint)pc;
    EmitWord(rom, ref pc, 0x10C1); // MOVE.B D1,(A0)+
    EmitWord(rom, ref pc, 0x51C8); // DBF D0,loop
    EmitWord(rom, ref pc, 0xFFFC);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 4; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(loopPc, machine.MainCpu.PC);
    AssertTrue(machine.MainCpu.TryFastForwardMoveBytePostIncrementDbfLoop(36, out int cycles, out int instructions), "fast-forward should recognize the fill loop");
    AssertEqual(36, cycles);
    AssertEqual(4, instructions);
    AssertEqual(0x00FF_1002u, machine.MainCpu.A[0]);
    AssertEqual(1u, machine.MainCpu.D[0] & 0xFFFF);
    AssertEqual((byte)0x7F, machine.Bus.ReadByte(0x00FF_1000));
    AssertEqual((byte)0x7F, machine.Bus.ReadByte(0x00FF_1001));
    AssertEqual((byte)0x00, machine.Bus.ReadByte(0x00FF_1002));

    for (int i = 0; i < 5; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0x00FF_1004u, machine.MainCpu.A[0]);
    AssertEqual(0xFFFFu, machine.MainCpu.D[0] & 0xFFFF);
    AssertEqual((byte)0x7F, machine.Bus.ReadByte(0x00FF_1002));
    AssertEqual((byte)0x7F, machine.Bus.ReadByte(0x00FF_1003));
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after the fill loop");
}

void M68kTstLongBneWaitLoopFastForward()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    uint loopPc = (uint)pc;
    EmitWord(rom, ref pc, 0x4A79); // TST.L $00FFCAD0
    EmitLong(rom, ref pc, 0x00FF_CAD0);
    EmitWord(rom, ref pc, 0x66F8); // BNE loop
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.Bus.WriteLong(0x00FF_CAD0, 0x0000_0001);

    AssertEqual(loopPc, machine.MainCpu.PC);
    AssertTrue(
        machine.MainCpu.TryFastForwardLongAbsoluteTstBneWaitLoop(56, address => (address & 0x00FF_0000u) == 0x00FF_0000u, out int cycles, out int instructions),
        "fast-forward should recognize the absolute work-RAM wait loop");
    AssertEqual(56, cycles);
    AssertEqual(8, instructions);
    AssertEqual(loopPc, machine.MainCpu.PC);
    AssertEqual(0x0000_0001u, machine.Bus.ReadLong(0x00FF_CAD0));
    AssertTrue((machine.MainCpu.SR & 0x0004) == 0, "nonzero TST result should leave Z clear");

    machine.Bus.WriteLong(0x00FF_CAD0, 0);
    AssertTrue(
        !machine.MainCpu.TryFastForwardLongAbsoluteTstBneWaitLoop(56, _ => true, out _, out _),
        "zero wait flag should fall back to the interpreter so the branch can exit");
}

void MovemPredecrementStoresOriginalAddressRegister()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x2E7C); // MOVEA.L #$00FF2000,A7
    EmitLong(rom, ref pc, 0x00FF_2000);
    EmitWord(rom, ref pc, 0x48E7); // MOVEM.L A7,-(A7)
    EmitWord(rom, ref pc, 0x0001);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 4; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0x00FF_1FFCu, machine.MainCpu.A[7]);
    AssertEqual(0x00FF_2000u, machine.Bus.ReadLong(0x00FF_1FFC));
}

void MultiplyInstructions()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x7006); // MOVEQ #6,D0
    EmitWord(rom, ref pc, 0x7207); // MOVEQ #7,D1
    EmitWord(rom, ref pc, 0xC2C0); // MULU.W D0,D1
    EmitWord(rom, ref pc, 0x7403); // MOVEQ #3,D2
    EmitWord(rom, ref pc, 0x7600); // MOVEQ #0,D3
    EmitWord(rom, ref pc, 0x363C); // MOVE.W #$FFFE,D3
    EmitWord(rom, ref pc, 0xFFFE);
    EmitWord(rom, ref pc, 0xC7C2); // MULS.W D2,D3
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 8; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(42u, machine.MainCpu.D[1]);
    AssertEqual(0xFFFF_FFFAu, machine.MainCpu.D[3]);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after multiply program");
}

void EorAndCmpmInstructions()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x700F); // MOVEQ #$0F,D0
    EmitWord(rom, ref pc, 0x7211); // MOVEQ #$11,D1
    EmitWord(rom, ref pc, 0xB101); // EOR.B D0,D1
    EmitWord(rom, ref pc, 0x207C); // MOVEA.L #$00FF1000,A0
    EmitLong(rom, ref pc, 0x00FF_1000);
    EmitWord(rom, ref pc, 0x227C); // MOVEA.L #$00FF1002,A1
    EmitLong(rom, ref pc, 0x00FF_1002);
    EmitWord(rom, ref pc, 0xB348); // CMPM.W (A0)+,(A1)+.
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.Bus.WriteWord(0x00FF_1000, 0x1234);
    machine.Bus.WriteWord(0x00FF_1002, 0x1234);
    for (int i = 0; i < 7; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0x1Eu, machine.MainCpu.D[1] & 0xFF);
    AssertEqual(0x00FF_1002u, machine.MainCpu.A[0]);
    AssertEqual(0x00FF_1004u, machine.MainCpu.A[1]);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after EOR/CMPM program");
}

void NegSccChkAndMovepInstructions()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x7C01); // MOVEQ #1,D6
    EmitWord(rom, ref pc, 0x4406); // NEG.B D6
    EmitWord(rom, ref pc, 0x7005); // MOVEQ #5,D0
    EmitWord(rom, ref pc, 0x7206); // MOVEQ #6,D1
    EmitWord(rom, ref pc, 0x4181); // CHK.W D1,D0
    EmitWord(rom, ref pc, 0x0C40); // CMPI.W #5,D0
    EmitWord(rom, ref pc, 0x0005);
    EmitWord(rom, ref pc, 0x57C2); // SEQ D2
    EmitWord(rom, ref pc, 0x207C); // MOVEA.L #$00FF1000,A0
    EmitLong(rom, ref pc, 0x00FF_1000);
    EmitWord(rom, ref pc, 0x263C); // MOVE.L #$A1B2C3D4,D3
    EmitLong(rom, ref pc, 0xA1B2_C3D4);
    EmitWord(rom, ref pc, 0x07C8); // MOVEP.L D3,0(A0)
    EmitWord(rom, ref pc, 0x0000);
    EmitWord(rom, ref pc, 0x2810); // MOVE.L (A0),D4, proving MOVEP is interleaved not linear.
    EmitWord(rom, ref pc, 0x2A3C); // MOVE.L #0,D5
    EmitLong(rom, ref pc, 0x0000_0000);
    EmitWord(rom, ref pc, 0x0B08); // MOVEP.W 0(A0),D5
    EmitWord(rom, ref pc, 0x0000);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 16; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0xFFu, machine.MainCpu.D[6] & 0xFF);
    AssertEqual(0xFFu, machine.MainCpu.D[2] & 0xFF);
    AssertEqual(0xA100B200u, machine.MainCpu.D[4]);
    AssertEqual(0xA1B2u, machine.MainCpu.D[5] & 0xFFFF);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after NEG/Scc/CHK/MOVEP program");
}

void MoveFromSrInstruction()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x40C0); // MOVE SR,D0
    EmitWord(rom, ref pc, 0x40F9); // MOVE SR,$FF1000
    EmitLong(rom, ref pc, 0x00FF_1000);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 3; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0x2700u, machine.MainCpu.D[0] & 0xFFFF);
    AssertEqual((ushort)0x2700, machine.Bus.ReadWord(0x00FF_1000));
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after MOVE from SR program");
}

void ExchangeTasAndBitOps()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x7001); // MOVEQ #1,D0
    EmitWord(rom, ref pc, 0x7202); // MOVEQ #2,D1
    EmitWord(rom, ref pc, 0xC141); // EXG D0,D1
    EmitWord(rom, ref pc, 0x7401); // MOVEQ #1,D2
    EmitWord(rom, ref pc, 0x05C1); // BSET D2,D1
    EmitWord(rom, ref pc, 0x0581); // BCLR D2,D1
    EmitWord(rom, ref pc, 0x0541); // BCHG D2,D1
    EmitWord(rom, ref pc, 0x13FC); // MOVE.B #$7F,$FF0100
    EmitWord(rom, ref pc, 0x007F);
    EmitLong(rom, ref pc, 0x00FF_0100);
    EmitWord(rom, ref pc, 0x4AF9); // TAS $FF0100
    EmitLong(rom, ref pc, 0x00FF_0100);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 12; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(2u, machine.MainCpu.D[0]);
    AssertEqual(3u, machine.MainCpu.D[1] & 0xFF);
    AssertEqual((byte)0xFF, machine.Bus.ReadByte(0x00FF_0100));
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after exchange/TAS/bit program");
}

void ImmediateBitWriteOps()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x7000); // MOVEQ #0,D0
    EmitWord(rom, ref pc, 0x08C0); // BSET #5,D0
    EmitWord(rom, ref pc, 0x0005);
    EmitWord(rom, ref pc, 0x0840); // BCHG #0,D0
    EmitWord(rom, ref pc, 0x0000);
    EmitWord(rom, ref pc, 0x207C); // MOVEA.L #$00FF1000,A0
    EmitLong(rom, ref pc, 0x00FF_1000);
    EmitWord(rom, ref pc, 0x08E8); // BSET #3,$0001(A0)
    EmitWord(rom, ref pc, 0x0003);
    EmitWord(rom, ref pc, 0x0001);
    EmitWord(rom, ref pc, 0x08A8); // BCLR #3,$0001(A0)
    EmitWord(rom, ref pc, 0x0003);
    EmitWord(rom, ref pc, 0x0001);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 8; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0x21u, machine.MainCpu.D[0] & 0xFF);
    AssertEqual((byte)0, machine.Bus.ReadByte(0x00FF_1001));
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after immediate bit write program");
}

void IllegalExceptionAndRte()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteLong(rom, 0x010, 0x0000_0300); // Illegal instruction vector.

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x00C0); // Invalid ORI encoding should vector through illegal instruction.
    EmitWord(rom, ref pc, 0x7007); // MOVEQ #7,D0 after RTE.
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    pc = 0x300;
    EmitWord(rom, ref pc, 0x7205); // MOVEQ #5,D1 in exception handler.
    EmitWord(rom, ref pc, 0x4E73); // RTE

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 5; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(7u, machine.MainCpu.D[0]);
    AssertEqual(5u, machine.MainCpu.D[1]);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after returning from illegal exception");
}

void IllegalOpcodeVectorsWithoutExtensionWord()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteLong(rom, 0x010, 0x0000_0300);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x4AFC); // ILLEGAL.
    EmitWord(rom, ref pc, 0x7007); // MOVEQ #7,D0 must run after RTE.
    EmitWord(rom, ref pc, 0x4E72);
    EmitWord(rom, ref pc, 0x2700);

    pc = 0x300;
    EmitWord(rom, ref pc, 0x7205);
    EmitWord(rom, ref pc, 0x4E73);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 5; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(7u, machine.MainCpu.D[0]);
    AssertEqual(5u, machine.MainCpu.D[1]);
    AssertTrue(machine.MainCpu.Stopped, "ILLEGAL should return to the following opcode, not consume an extension word");
}

void InvalidMoveaByteVectorsIllegal()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteLong(rom, 0x010, 0x0000_0300); // Illegal instruction vector.

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x1040); // Invalid MOVEA.B D0,A0 encoding.
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    pc = 0x300;
    EmitWord(rom, ref pc, 0x7004); // MOVEQ #4,D0
    EmitWord(rom, ref pc, 0x4E73); // RTE

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 5; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(4u, machine.MainCpu.D[0]);
    AssertTrue(machine.MainCpu.ExceptionCounts.ContainsKey(4), "invalid MOVEA.B should enter illegal instruction vector");
}

void InvalidEffectiveAddressVectorsIllegal()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteLong(rom, 0x010, 0x0000_0300);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x41C0); // LEA D0,A0 is an invalid effective-address form.
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700 if exception handling fails to redirect.
    EmitWord(rom, ref pc, 0x2700);

    pc = 0x300;
    EmitWord(rom, ref pc, 0x7004); // MOVEQ #4,D0
    EmitWord(rom, ref pc, 0x4E73); // RTE

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 4; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(4u, machine.MainCpu.D[0]);
    AssertTrue(machine.MainCpu.ExceptionCounts.ContainsKey(4), "invalid LEA effective address should enter illegal instruction vector");
    AssertTrue(machine.MainCpu.Stopped, "CPU should return from the exception handler and stop");
}

void RteReturnsToUserModeFromSupervisorStack()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteLong(rom, 0x010, 0x0000_0300); // Illegal instruction vector.

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x41F9); // LEA $00FE0000,A0
    EmitLong(rom, ref pc, 0x00FE_0000);
    EmitWord(rom, ref pc, 0x4E60); // MOVE A0,USP
    EmitWord(rom, ref pc, 0x027C); // ANDI #$D8FF,SR, enters user mode with interrupts unmasked.
    EmitWord(rom, ref pc, 0xD8FF);
    EmitWord(rom, ref pc, 0x4AFC); // ILLEGAL, vectors on supervisor stack.
    EmitWord(rom, ref pc, 0x7007); // MOVEQ #7,D0 after RTE.
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    pc = 0x300;
    EmitWord(rom, ref pc, 0x7205); // MOVEQ #5,D1 in exception handler.
    EmitWord(rom, ref pc, 0x4E73); // RTE

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 8; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(7u, machine.MainCpu.D[0]);
    AssertEqual(5u, machine.MainCpu.D[1]);
    AssertTrue(machine.MainCpu.Stopped, "CPU should resume user-mode code after RTE and stop");
}

void InterruptSwitchesFromUserStackToSupervisorStack()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteLong(rom, (24 + 6) * 4, 0x0000_0300); // Level 6 interrupt vector.
    WriteLong(rom, 0x020, 0x0000_0380); // Privilege violation vector.

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x41F9); // LEA $00FE0000,A0
    EmitLong(rom, ref pc, 0x00FE_0000);
    EmitWord(rom, ref pc, 0x4E60); // MOVE A0,USP
    EmitWord(rom, ref pc, 0x2E7C); // MOVEA.L #$00FD0000,A7
    EmitLong(rom, ref pc, 0x00FD_0000);
    EmitWord(rom, ref pc, 0x027C); // ANDI #$D8FF,SR, enters user mode with interrupts unmasked.
    EmitWord(rom, ref pc, 0xD8FF);
    EmitWord(rom, ref pc, 0x7007); // MOVEQ #7,D0 after interrupt returns.
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    pc = 0x300;
    EmitWord(rom, ref pc, 0x4E68); // MOVE USP,A0 proves interrupt handler is supervisor-mode.
    EmitWord(rom, ref pc, 0x7205); // MOVEQ #5,D1
    EmitWord(rom, ref pc, 0x4E73); // RTE

    pc = 0x380;
    EmitWord(rom, ref pc, 0x7409); // MOVEQ #9,D2 if interrupt handler wrongly stayed in user mode.
    EmitWord(rom, ref pc, 0x4E73); // RTE

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 4; i++)
    {
        machine.StepInstruction();
    }

    machine.MainCpu.RequestInterrupt(6);
    for (int i = 0; i < 4; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(7u, machine.MainCpu.D[0]);
    AssertEqual(5u, machine.MainCpu.D[1]);
    AssertEqual(0u, machine.MainCpu.D[2]);
    AssertEqual(0x00FE_0000u, machine.MainCpu.A[0]);
    AssertEqual(0x00FE_0000u, machine.MainCpu.A[7]);
    AssertTrue((machine.MainCpu.SR & 0x2000) == 0, "RTE should restore user mode after interrupt");
}

void MoveUspDirectionsAndPrivilege()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteLong(rom, 0x020, 0x0000_0300); // Privilege violation vector.

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x41F9); // LEA $00FE0000,A0
    EmitLong(rom, ref pc, 0x00FE_0000);
    EmitWord(rom, ref pc, 0x4E60); // MOVE A0,USP
    EmitWord(rom, ref pc, 0x43F9); // LEA $00FD0000,A1
    EmitLong(rom, ref pc, 0x00FD_0000);
    EmitWord(rom, ref pc, 0x4E69); // MOVE USP,A1
    EmitWord(rom, ref pc, 0x027C); // ANDI #$DFFF,SR, enters user mode.
    EmitWord(rom, ref pc, 0xDFFF);
    EmitWord(rom, ref pc, 0x4E60); // MOVE A0,USP is privileged in user mode.

    pc = 0x300;
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 7; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0x00FE_0000u, machine.MainCpu.A[1]);
    AssertTrue(machine.MainCpu.ExceptionCounts.ContainsKey(8), "user-mode MOVE USP should enter privilege violation vector");
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop in the privilege violation handler");
}

void RtrRestoresCcrAndPc()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x007C); // ORI #$001F,SR
    EmitWord(rom, ref pc, 0x001F);
    EmitWord(rom, ref pc, 0x4879); // PEA $00000220
    EmitLong(rom, ref pc, 0x0000_0220);
    EmitWord(rom, ref pc, 0x3F3C); // MOVE.W #$0004,-(A7)
    EmitWord(rom, ref pc, 0x0004);
    EmitWord(rom, ref pc, 0x4E77); // RTR
    EmitWord(rom, ref pc, 0x7001); // Should be skipped.

    pc = 0x220;
    EmitWord(rom, ref pc, 0x6702); // BEQ +2, proves Z was restored.
    EmitWord(rom, ref pc, 0x7002); // Should be skipped.
    EmitWord(rom, ref pc, 0x7007); // MOVEQ #7,D0
    EmitWord(rom, ref pc, 0x4E72);
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 8; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(7u, machine.MainCpu.D[0]);
    AssertTrue((machine.MainCpu.SR & 0x001F) != 0x001F, "RTR should replace CCR rather than leave old flags intact");
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after RTR target code");
}

void ResetInstructionIsPrivilegedAndResumes()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteLong(rom, 0x020, 0x0000_0300); // Privilege violation vector.

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x4E70); // RESET is legal in supervisor mode.
    EmitWord(rom, ref pc, 0x7007); // MOVEQ #7,D0
    EmitWord(rom, ref pc, 0x027C); // ANDI #$DFFF,SR, enters user mode.
    EmitWord(rom, ref pc, 0xDFFF);
    EmitWord(rom, ref pc, 0x4E70); // RESET in user mode vectors privilege violation.
    EmitWord(rom, ref pc, 0x4E72);
    EmitWord(rom, ref pc, 0x2700);

    pc = 0x300;
    EmitWord(rom, ref pc, 0x7205); // MOVEQ #5,D1 in privilege handler.
    EmitWord(rom, ref pc, 0x4E73); // RTE

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 8; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(7u, machine.MainCpu.D[0]);
    AssertEqual(5u, machine.MainCpu.D[1]);
    AssertTrue(machine.MainCpu.ExceptionCounts.ContainsKey(8), "user-mode RESET should enter privilege violation vector");
    AssertTrue(machine.MainCpu.Stopped, "CPU should resume after RESET privilege exception and stop");
}

void MovePostincrementToVdpAbsoluteLong()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x33FC); // MOVE.W #$8F02,$C00004
    EmitWord(rom, ref pc, 0x8F02);
    EmitLong(rom, ref pc, 0x00C0_0004);
    EmitWord(rom, ref pc, 0x23FC); // MOVE.L #$40000000,$C00004
    EmitLong(rom, ref pc, 0x4000_0000);
    EmitLong(rom, ref pc, 0x00C0_0004);
    EmitWord(rom, ref pc, 0x287C); // MOVEA.L #$00FF0000,A4
    EmitLong(rom, ref pc, 0x00FF_0000);
    EmitWord(rom, ref pc, 0x33DC); // MOVE.W (A4)+,$C00000
    EmitLong(rom, ref pc, 0x00C0_0000);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.Bus.WriteWord(0x00FF_0000, 0x1234);
    for (int i = 0; i < 5; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual((byte)0x12, machine.Vdp.Vram[0]);
    AssertEqual((byte)0x34, machine.Vdp.Vram[1]);
    AssertEqual(0x00FF_0002u, machine.MainCpu.A[4]);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after MOVE.W to VDP");
}

void DbraUsesDisplacementWordOrigin()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x33FC); // MOVE.W #$8F02,$C00004
    EmitWord(rom, ref pc, 0x8F02);
    EmitLong(rom, ref pc, 0x00C0_0004);
    EmitWord(rom, ref pc, 0x23FC); // MOVE.L #$40000000,$C00004
    EmitLong(rom, ref pc, 0x4000_0000);
    EmitLong(rom, ref pc, 0x00C0_0004);
    EmitWord(rom, ref pc, 0x303C); // MOVE.W #2,D0
    EmitWord(rom, ref pc, 0x0002);
    EmitWord(rom, ref pc, 0x287C); // MOVEA.L #$00FF0000,A4
    EmitLong(rom, ref pc, 0x00FF_0000);
    EmitWord(rom, ref pc, 0x33DC); // MOVE.W (A4)+,$C00000
    EmitLong(rom, ref pc, 0x00C0_0000);
    EmitWord(rom, ref pc, 0x51C8); // DBRA D0 back to the MOVE.W.
    EmitWord(rom, ref pc, 0xFFF8);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.Bus.WriteWord(0x00FF_0000, 0x1111);
    machine.Bus.WriteWord(0x00FF_0002, 0x2222);
    machine.Bus.WriteWord(0x00FF_0004, 0x3333);

    for (int i = 0; i < 17; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual((byte)0x11, machine.Vdp.Vram[0]);
    AssertEqual((byte)0x11, machine.Vdp.Vram[1]);
    AssertEqual((byte)0x22, machine.Vdp.Vram[2]);
    AssertEqual((byte)0x22, machine.Vdp.Vram[3]);
    AssertEqual((byte)0x33, machine.Vdp.Vram[4]);
    AssertEqual((byte)0x33, machine.Vdp.Vram[5]);
    AssertEqual(0x00FF_0006u, machine.MainCpu.A[4]);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after DBRA loop");
}

void BraWordUsesDisplacementWordOrigin()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x6000); // BRA.W to $0208 from extension word at $0202.
    EmitWord(rom, ref pc, 0x0006);
    EmitWord(rom, ref pc, 0x7001); // skipped if the displacement origin is correct.
    EmitWord(rom, ref pc, 0x7002); // skipped if the displacement origin is PC after extension word.
    EmitWord(rom, ref pc, 0x7003); // target.
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 3; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(3u, machine.MainCpu.D[0]);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after BRA.W target");
}

void ProgramCounterUses24BitAddressBus()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x4EB9); // JSR $06000300, high byte should not appear on the 68000 address bus.
    EmitLong(rom, ref pc, 0x0600_0300);
    EmitWord(rom, ref pc, 0x7201); // MOVEQ #1,D1
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    pc = 0x300;
    EmitWord(rom, ref pc, 0x7007); // MOVEQ #7,D0
    EmitWord(rom, ref pc, 0x4E75); // RTS

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 5; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(7u, machine.MainCpu.D[0]);
    AssertEqual(1u, machine.MainCpu.D[1]);
    AssertTrue(machine.MainCpu.Stopped, "CPU should return from a high-byte absolute JSR through the 24-bit bus address");
}

void PcRelativeEffectiveAddressUsesExtensionWordOrigin()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x41FA); // LEA label(PC),A0; extension word is at $0202.
    EmitWord(rom, ref pc, 0x001E);
    EmitWord(rom, ref pc, 0x303A); // MOVE.W label(PC),D0; extension word is at $0206.
    EmitWord(rom, ref pc, 0x001A);
    EmitWord(rom, ref pc, 0x7202); // MOVEQ #2,D1 for indexed PC-relative addressing.
    EmitWord(rom, ref pc, 0x343B); // MOVE.W label2(PC,D1.W),D2; extension word is at $020C.
    EmitWord(rom, ref pc, 0x1014);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);
    WriteWord(rom, 0x220, 0xBEEF);
    WriteWord(rom, 0x222, 0xCAFE);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 5; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0x0000_0220u, machine.MainCpu.A[0]);
    AssertEqual(0xBEEFu, machine.MainCpu.D[0] & 0xFFFF);
    AssertEqual(0xCAFEu, machine.MainCpu.D[2] & 0xFFFF);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after PC-relative EA checks");
}

void ImmediateRmwAbsoluteLongEvaluatesEaOnce()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x0639); // ADDI.B #1,$FF00FF
    EmitWord(rom, ref pc, 0x0001);
    EmitLong(rom, ref pc, 0x00FF_00FF);
    EmitWord(rom, ref pc, 0x4E72); // STOP #$2700
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    machine.Bus.WriteByte(0x00FF_00FF, 0x07);
    for (int i = 0; i < 4; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual((byte)0x08, machine.Bus.ReadByte(0x00FF_00FF));
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after ADDI.B absolute long");
}

void AsrSignExtendsRegisterOperands()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);
    WriteWord(rom, 0x200, 0x72E8); // MOVEQ #-24,D1
    WriteWord(rom, 0x202, 0xEA41); // ASR.W #5,D1
    WriteWord(rom, 0x204, 0x7480); // MOVEQ #-128,D2
    WriteWord(rom, 0x206, 0xE202); // ASR.B #1,D2
    WriteWord(rom, 0x208, 0x4E72);
    WriteWord(rom, 0x20A, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 5; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0xFFFF_FFFFu, machine.MainCpu.D[1]);
    AssertEqual(0xFFFF_FFC0u, machine.MainCpu.D[2]);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after ASR sign-extension test");
}

void RegisterShiftCountZeroIsNoOp()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x7800); // MOVEQ #0,D4
    EmitWord(rom, ref pc, 0x343C); // MOVE.W #$000E,D2
    EmitWord(rom, ref pc, 0x000E);
    EmitWord(rom, ref pc, 0xE86A); // LSR.W D4,D2: register count zero leaves D2 unchanged.
    EmitWord(rom, ref pc, 0x2602); // MOVE.L D2,D3
    EmitWord(rom, ref pc, 0x343C); // MOVE.W #$0100,D2
    EmitWord(rom, ref pc, 0x0100);
    EmitWord(rom, ref pc, 0xE04A); // LSR.W #8,D2: immediate count zero encodes eight.
    EmitWord(rom, ref pc, 0x4E72);
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 8; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0u, machine.MainCpu.D[4]);
    AssertEqual(0x0000_000Eu, machine.MainCpu.D[3]);
    AssertEqual(0x0000_0001u, machine.MainCpu.D[2]);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after shift-count test");
}

void RegisterShiftCountsAboveOperandWidth()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x7809); // MOVEQ #9,D4
    EmitWord(rom, ref pc, 0x7401); // MOVEQ #1,D2
    EmitWord(rom, ref pc, 0xE92A); // LSL.B D4,D2: count 9 must not become count 1.
    EmitWord(rom, ref pc, 0x2A02); // MOVE.L D2,D5
    EmitWord(rom, ref pc, 0x7600); // MOVEQ #0,D3
    EmitWord(rom, ref pc, 0x363C); // MOVE.W #$8000,D3
    EmitWord(rom, ref pc, 0x8000);
    EmitWord(rom, ref pc, 0x7810); // MOVEQ #16,D4
    EmitWord(rom, ref pc, 0xE86B); // LSR.W D4,D3.
    EmitWord(rom, ref pc, 0x40C0); // MOVE SR,D0.
    EmitWord(rom, ref pc, 0x0240); // ANDI.W #$001F,D0 copies CCR after LSR.W.
    EmitWord(rom, ref pc, 0x001F);
    EmitWord(rom, ref pc, 0x7480); // MOVEQ #-128,D2
    EmitWord(rom, ref pc, 0x7809); // MOVEQ #9,D4
    EmitWord(rom, ref pc, 0xE822); // ASR.B D4,D2.
    EmitWord(rom, ref pc, 0x4E72);
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 14; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0u, machine.MainCpu.D[5] & 0xFF);
    AssertEqual(0u, machine.MainCpu.D[3] & 0xFFFF);
    AssertEqual(0x0015u, machine.MainCpu.D[0] & 0x001F); // X, Z, and C set after LSR.W #16.
    AssertEqual(0xFFFF_FFFFu, machine.MainCpu.D[2]);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after wide shift-count test");
}

void RotateAndArithmeticShiftFlags()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x46FC); // MOVE #$2710,SR: X set.
    EmitWord(rom, ref pc, 0x2710);
    EmitWord(rom, ref pc, 0x7401); // MOVEQ #1,D2
    EmitWord(rom, ref pc, 0xE31A); // ROL.B #1,D2: carry clears, X is unchanged.
    EmitWord(rom, ref pc, 0x2A02); // MOVE.L D2,D5
    EmitWord(rom, ref pc, 0x40C0); // MOVE SR,D0.
    EmitWord(rom, ref pc, 0x0240); // ANDI.W #$001F,D0 copies CCR after ROL.B.
    EmitWord(rom, ref pc, 0x001F);
    EmitWord(rom, ref pc, 0x7440); // MOVEQ #$40,D2
    EmitWord(rom, ref pc, 0xE302); // ASL.B #1,D2: sign changes, so V is set.
    EmitWord(rom, ref pc, 0x40C1); // MOVE SR,D1.
    EmitWord(rom, ref pc, 0x0241); // ANDI.W #$001F,D1 copies CCR after ASL.B.
    EmitWord(rom, ref pc, 0x001F);
    EmitWord(rom, ref pc, 0x7680); // MOVEQ #-128,D3
    EmitWord(rom, ref pc, 0xE513); // ROXL.B #2,D3: the second step must see X from the first step.
    EmitWord(rom, ref pc, 0x4E72);
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 15; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0x0002u, machine.MainCpu.D[5] & 0xFF);
    AssertEqual(0x0010u, machine.MainCpu.D[0] & 0x001F);
    AssertEqual(0x000Au, machine.MainCpu.D[1] & 0x001F); // N and V set after ASL.B $40.
    AssertEqual(0x0001u, machine.MainCpu.D[3] & 0xFF);
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after rotate flag test");
}

void AddSubXInstructions()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x2C3C); // MOVE.L #$80000000,D6
    EmitLong(rom, ref pc, 0x8000_0000);
    EmitWord(rom, ref pc, 0x007C); // ORI #$0014,SR: X and Z set.
    EmitWord(rom, ref pc, 0x0014);
    EmitWord(rom, ref pc, 0xDD86); // ADDX.L D6,D6.
    EmitWord(rom, ref pc, 0x2C3C); // MOVE.L #0,D6
    EmitLong(rom, ref pc, 0);
    EmitWord(rom, ref pc, 0x46FC); // MOVE #$2704,SR: Z set, X clear.
    EmitWord(rom, ref pc, 0x2704);
    EmitWord(rom, ref pc, 0xDD86); // ADDX.L D6,D6 preserves Z when the result is zero.
    EmitWord(rom, ref pc, 0x303C); // MOVE.W #$0001,D0
    EmitWord(rom, ref pc, 0x0001);
    EmitWord(rom, ref pc, 0x323C); // MOVE.W #$0000,D1
    EmitWord(rom, ref pc, 0x0000);
    EmitWord(rom, ref pc, 0x46FC); // MOVE #$2710,SR: X set.
    EmitWord(rom, ref pc, 0x2710);
    EmitWord(rom, ref pc, 0x9300); // SUBX.B D0,D1.
    EmitWord(rom, ref pc, 0x4E72);
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();

    machine.StepInstruction();
    machine.StepInstruction();
    machine.StepInstruction();
    AssertEqual(0x0000_0001u, machine.MainCpu.D[6]);
    AssertEqual((ushort)0x2713, machine.MainCpu.SR);

    machine.StepInstruction();
    machine.StepInstruction();
    machine.StepInstruction();
    AssertEqual(0u, machine.MainCpu.D[6]);
    AssertEqual((ushort)0x2704, machine.MainCpu.SR);

    for (int i = 0; i < 4; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual(0x0000_00FEu, machine.MainCpu.D[1] & 0xFF);
    AssertEqual((ushort)0x2719, machine.MainCpu.SR);
    machine.StepInstruction();
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after ADDX/SUBX test");
}

void BcdArithmeticInstructions()
{
    byte[] rom = CreateRom();
    WriteLong(rom, 0x000, 0x00FF_0000);
    WriteLong(rom, 0x004, 0x0000_0200);

    int pc = 0x200;
    EmitWord(rom, ref pc, 0x2A7C); // MOVEA.L #$00FF0101,A5
    EmitLong(rom, ref pc, 0x00FF_0101);
    EmitWord(rom, ref pc, 0x2C7C); // MOVEA.L #$00FF0201,A6
    EmitLong(rom, ref pc, 0x00FF_0201);
    EmitWord(rom, ref pc, 0x13FC); // MOVE.B #$99,$00FF0100
    EmitWord(rom, ref pc, 0x0099);
    EmitLong(rom, ref pc, 0x00FF_0100);
    EmitWord(rom, ref pc, 0x13FC); // MOVE.B #$01,$00FF0200
    EmitWord(rom, ref pc, 0x0001);
    EmitLong(rom, ref pc, 0x00FF_0200);
    EmitWord(rom, ref pc, 0x46FC); // MOVE #$2714,SR: X and Z set for multi-byte ABCD.
    EmitWord(rom, ref pc, 0x2714);
    EmitWord(rom, ref pc, 0xCD0D); // ABCD -(A5),-(A6): 99 + 01 + X => 01, carry set.
    EmitWord(rom, ref pc, 0x7001); // MOVEQ #1,D0
    EmitWord(rom, ref pc, 0x7200); // MOVEQ #0,D1
    EmitWord(rom, ref pc, 0x46FC); // MOVE #$2710,SR: X set.
    EmitWord(rom, ref pc, 0x2710);
    EmitWord(rom, ref pc, 0x8300); // SBCD D0,D1: 00 - 01 - X => 98, borrow set.
    EmitWord(rom, ref pc, 0x4E72);
    EmitWord(rom, ref pc, 0x2700);

    MegaDrive machine = new(CartridgeImage.FromBytes(rom));
    machine.Reset();
    for (int i = 0; i < 10; i++)
    {
        machine.StepInstruction();
    }

    AssertEqual((byte)0x01, machine.Bus.ReadByte(0x00FF_0200));
    AssertEqual(0x00FF_0100u, machine.MainCpu.A[5]);
    AssertEqual(0x00FF_0200u, machine.MainCpu.A[6]);
    AssertEqual(0x0000_0098u, machine.MainCpu.D[1] & 0xFF);
    AssertEqual((ushort)0x2719, machine.MainCpu.SR);
    machine.StepInstruction();
    AssertTrue(machine.MainCpu.Stopped, "CPU should stop after BCD arithmetic test");
}

void VdpFrameRendererDrawsPlaneTiles()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x8F02); // Auto-increment by one word.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8200); // Plane A table at $0000.
    vdp.WriteControlPort(0x8D02); // Horizontal scroll table at $0800.

    vdp.WriteControlPort(0xC002); // CRAM write at color 1.
    vdp.WriteControlPort(0x0000);
    vdp.WriteDataPort(0x000E); // Bright red.

    vdp.WriteControlPort(0x4020); // VRAM write at tile 1 data.
    vdp.WriteControlPort(0x0000);
    for (int i = 0; i < 8; i++)
    {
        vdp.WriteDataPort(0x1111);
        vdp.WriteDataPort(0x1111);
    }

    vdp.WriteControlPort(0x4000); // VRAM write at name table entry 0.
    vdp.WriteControlPort(0x0000);
    vdp.WriteDataPort(0x0001);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "first pixel should be red");
    AssertEqual((byte)0, frame[1]);
    AssertEqual((byte)0, frame[2]);
}

void VdpPlanePixelTraceMapsTileSource()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x8F02); // Auto-increment by one word.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8200); // Plane A table at $0000.
    vdp.WriteControlPort(0x8D02); // Horizontal scroll table at $0800.

    vdp.WriteControlPort(0x4020); // VRAM write at tile 1 data.
    vdp.WriteControlPort(0x0000);
    vdp.WriteDataPort(0x1234);

    vdp.WriteControlPort(0x4000); // VRAM write at name table entry 0.
    vdp.WriteControlPort(0x0000);
    vdp.WriteDataPort(0x0001);

    Vdp.VdpPlanePixelTrace trace = vdp.TracePlanePixel(VdpDebugLayer.PlaneA, 2, 0);
    AssertEqual((int)VdpPlaneSourceKind.Plane, (int)trace.SourceKind);
    AssertEqual(0, trace.NameTableAddress);
    AssertEqual(0x0001, trace.Name);
    AssertEqual(1, trace.TileIndex);
    AssertEqual(0x0021, trace.TileAddress);
    AssertEqual(2, trace.PixelX);
    AssertEqual(0, trace.PixelY);
    AssertEqual(3, trace.ColorIndex);
    AssertEqual((byte)0x34, trace.PackedByte);
}

void VdpInterlaceDoubleModeUsesTallTiles()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C87); // H40 mode with interlace double resolution.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8200); // Plane A table at $0000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteCramColor(vdp, 2, 0x00E0);
    WriteTallTile(vdp, tile: 1, topColor: 1, bottomColor: 2);
    WriteVramWordAt(vdp, 0x0000, 0x0001);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "top half of an interlace tile should render from the first eight rows");
    int lower = PixelOffset(0, 4);
    AssertTrue(frame[lower + 1] > 200, "interlace mode should fetch the second eight tile rows before the next nametable cell");
    AssertEqual((byte)0, frame[lower]);
    AssertEqual((byte)0, frame[lower + 2]);
}

void VdpFrameRendererDrawsSprites()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x8F02); // Auto-increment by one word.
    vdp.WriteControlPort(0x8500); // Sprite attribute table at $0000.

    vdp.WriteControlPort(0xC002); // CRAM write at color 1.
    vdp.WriteControlPort(0x0000);
    vdp.WriteDataPort(0x000E); // Bright red.

    vdp.WriteControlPort(0x4020); // VRAM write at tile 1 data.
    vdp.WriteControlPort(0x0000);
    for (int i = 0; i < 8; i++)
    {
        vdp.WriteDataPort(0x1111);
        vdp.WriteDataPort(0x1111);
    }

    vdp.WriteControlPort(0x4000); // Sprite 0: 1x1 tile at screen origin, link end.
    vdp.WriteControlPort(0x0000);
    vdp.WriteDataPort(0x0080);
    vdp.WriteDataPort(0x0000);
    vdp.WriteDataPort(0x0001);
    vdp.WriteDataPort(0x0080);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "sprite pixel should be red");
    AssertEqual((byte)0, frame[1]);
    AssertEqual((byte)0, frame[2]);
}

void VdpH32SpritesUseActiveDisplayCoordinates()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C00); // H32 mode.
    vdp.WriteControlPort(0x8500); // Sprite attribute table at $0000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteSprite(vdp, 0x0000, y: 0, x: 0, tile: 1, link: 0);

    byte[] frame = vdp.RenderFrameRgb();
    int leftBorder = PixelOffset(0, 0);
    AssertEqual((byte)0, frame[leftBorder]);
    AssertEqual((byte)0, frame[leftBorder + 1]);
    AssertEqual((byte)0, frame[leftBorder + 2]);

    int activeOrigin = PixelOffset(32, 0);
    AssertTrue(frame[activeOrigin] > 200, "H32 sprite X should be relative to the 256-pixel active display, not the centered output frame");
    AssertEqual((byte)0, frame[activeOrigin + 1]);
    AssertEqual((byte)0, frame[activeOrigin + 2]);
}

void VdpInterlaceSpritesUseSourceCoordinates()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C87); // H40 mode with interlace double resolution.
    vdp.WriteControlPort(0x8500); // Sprite attribute table at $0000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteTallTile(vdp, tile: 1, topColor: 1, bottomColor: 1);
    WriteInterlaceSpriteWithSize(vdp, 0x0000, y: 8, x: 0, tile: 1, sizeLink: 0);

    byte[] frame = vdp.RenderFrameRgb();
    int tooHigh = PixelOffset(0, 4);
    AssertTrue(frame[tooHigh] > 100, "interlace sprite placement should project doubled source coordinates into display coordinates");
    int tooLow = PixelOffset(0, 12);
    AssertEqual((byte)0, frame[tooLow]);
    AssertEqual((byte)0, frame[tooLow + 1]);
    AssertEqual((byte)0, frame[tooLow + 2]);
}

void VdpSpriteYCoordinateIgnoresHighBits()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x8502); // Sprite table at $0400.

    WriteCramColor(vdp, 1, 0x000E);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteSprite(vdp, 0x0400, y: 16, x: 24, tile: 1, link: 0);
    WriteVramWordAt(vdp, 0x0400, 0xFE90); // Low nine bits still represent Y=16.

    byte[] frame = vdp.RenderFrameRgb();
    int pixel = PixelOffset(24, 16);
    AssertTrue(frame[pixel] > 200, "normal-mode sprite Y should ignore high attribute bits");
    AssertEqual((byte)0, frame[pixel + 1]);
    AssertEqual((byte)0, frame[pixel + 2]);
}

void VdpFrameRendererUsesPerLineSpriteSnapshots()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C87); // H40 mode with interlace double resolution.
    vdp.WriteControlPort(0x8500); // Sprite attribute table at $0000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteCramColor(vdp, 2, 0x00E0);
    WriteTallTile(vdp, tile: 1, topColor: 1, bottomColor: 1);
    WriteTallTile(vdp, tile: 2, topColor: 2, bottomColor: 2);

    vdp.BeginFrame(pal: false);
    WriteInterlaceSpriteWithSize(vdp, 0x0000, y: 0, x: 0, tile: 1, sizeLink: 0);
    vdp.StepScanline(0, pal: false);
    WriteInterlaceSpriteWithSize(vdp, 0x0000, y: 240, x: 0, tile: 2, sizeLink: 0);
    vdp.StepScanline(120, pal: false);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "line zero should use the sprite table captured before the mid-frame SAT rewrite");
    AssertEqual((byte)0, frame[1]);

    int lower = PixelOffset(0, 120);
    AssertTrue(frame[lower + 1] > 200, "later lines should use their own captured sprite table");
    AssertEqual((byte)0, frame[lower]);
    AssertEqual((byte)0, frame[lower + 2]);
}

void VdpFrameRendererUsesActiveFrameVramSnapshot()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x8500); // Sprite attribute table at $0000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteCramColor(vdp, 2, 0x00E0);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteSolidTile(vdp, tile: 2, color: 2);

    vdp.BeginFrame(pal: false);
    WriteSprite(vdp, 0x0000, y: 0, x: 0, tile: 1, link: 0);
    vdp.StepScanline(0, pal: false);
    vdp.StepScanline(224, pal: false);

    WriteSolidTile(vdp, tile: 1, color: 2);
    WriteSprite(vdp, 0x0000, y: 0, x: 0, tile: 2, link: 0);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "rendering after VBlank VRAM updates should use the active frame's sprite tile data");
    AssertEqual((byte)0, frame[1]);
    AssertEqual((byte)0, frame[2]);
}

void VdpFrameRendererUsesPerLinePlaneVramSnapshots()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8230); // Plane A table at $C000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteCramColor(vdp, 2, 0x00E0);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteVramWordAt(vdp, 0xC000, 0x0001);
    WriteVramWordAt(vdp, 0xC040, 0x0001);

    vdp.BeginFrame(pal: false);
    vdp.StepScanline(0, pal: false);
    WriteSolidTile(vdp, tile: 1, color: 2);
    vdp.StepScanline(8, pal: false);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "line zero should use plane tile data captured before the mid-frame pattern rewrite");
    AssertEqual((byte)0, frame[1]);
    AssertEqual((byte)0, frame[2]);

    int lineEight = PixelOffset(0, 8);
    AssertTrue(frame[lineEight + 1] > 200, "later lines should use their own captured plane tile data");
    AssertEqual((byte)0, frame[lineEight]);
    AssertEqual((byte)0, frame[lineEight + 2]);
}

void VdpDmaTimingSnapshotsPreservePartialVram()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8230); // Plane A table at $C000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteCramColor(vdp, 2, 0x00E0);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteVramWordAt(vdp, 0xC000, 0x0001);
    WriteVramWordAt(vdp, 0xC040, 0x0001);

    vdp.BeginFrame(pal: false);
    vdp.StepScanline(0, pal: false);
    vdp.CaptureLineVramForDmaTiming(8);
    WriteSolidTile(vdp, tile: 1, color: 2);
    vdp.StepScanline(8, pal: false);

    byte[] frame = vdp.RenderFrameRgb();
    int lineEight = PixelOffset(0, 8);
    AssertTrue(frame[lineEight] > 200, "DMA timing capture should preserve the line's earlier VRAM state");
    AssertEqual((byte)0, frame[lineEight + 1]);
    AssertEqual((byte)0, frame[lineEight + 2]);
}

void VdpFrameRendererUsesPerLineSpritePatternSnapshots()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x8500); // Sprite attribute table at $0000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteCramColor(vdp, 2, 0x00E0);
    WriteSolidTile(vdp, tile: 1, color: 1);

    vdp.BeginFrame(pal: false);
    WriteSprite(vdp, 0x0000, y: 0, x: 0, tile: 1, link: 0);
    vdp.StepScanline(0, pal: false);

    WriteSolidTile(vdp, tile: 1, color: 2);
    vdp.StepScanline(224, pal: false);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "sprite pixels already drawn on a line should not pick up later pattern table writes");
    AssertEqual((byte)0, frame[1]);
    AssertEqual((byte)0, frame[2]);
}

void VdpFrameRendererDrawsMultiCellSpritesInVdpOrder()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x8F02); // Auto-increment by one word.
    vdp.WriteControlPort(0x8500); // Sprite attribute table at $0000.

    WriteCramColor(vdp, 1, 0x000E); // Red.
    WriteCramColor(vdp, 2, 0x00E0); // Green.
    WriteCramColor(vdp, 3, 0x0E00); // Blue.
    WriteCramColor(vdp, 4, 0x0EEE); // White.
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteSolidTile(vdp, tile: 2, color: 2);
    WriteSolidTile(vdp, tile: 3, color: 3);
    WriteSolidTile(vdp, tile: 4, color: 4);

    int twoByThree = (1 << 10) | (2 << 8);
    WriteSpriteWithSize(vdp, 0x0000, y: 0, x: 0, tile: 1, sizeLink: twoByThree);

    byte[] frame = vdp.RenderFrameRgb();
    int topRight = PixelOffset(9, 1);
    AssertTrue(frame[topRight] > 200 && frame[topRight + 1] > 200 && frame[topRight + 2] > 200, "right column should use the fourth tile");

    int lowerLeft = PixelOffset(1, 17);
    AssertTrue(frame[lowerLeft + 2] > 200, "third row of left column should use the third tile");
    AssertEqual((byte)0, frame[lowerLeft]);
    AssertEqual((byte)0, frame[lowerLeft + 1]);
}

void VdpFrameRendererBlanksWhenDisplayIsDisabled()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8F02); // Auto-increment by one word.

    vdp.WriteControlPort(0x4020); // VRAM write at tile 1 data.
    vdp.WriteControlPort(0x0000);
    for (int i = 0; i < 8; i++)
    {
        vdp.WriteDataPort(0x1111);
        vdp.WriteDataPort(0x1111);
    }

    byte[] frame = vdp.RenderFrameRgb();
    AssertEqual("display-disabled", vdp.LastRenderMode);
    AssertTrue(frame.All(value => value == 0), "disabled display should show only the background color");
}

void VdpFrameRendererAppliesScroll()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8230); // Plane A table at $C000.
    vdp.WriteControlPort(0x8407); // Plane B table at $E000.
    vdp.WriteControlPort(0x8D02); // Horizontal scroll table at $0800.

    WriteCramColor(vdp, 1, 0x000E);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteVramWordAt(vdp, 0xC000, 0x0001);
    WriteVramWordAt(vdp, 0x0800, 0x0008); // Positive H-scroll moves the plane right.

    byte[] frame = vdp.RenderFrameRgb();
    AssertEqual((byte)0, frame[0]);
    AssertEqual((byte)0, frame[1]);
    AssertEqual((byte)0, frame[2]);
    int shifted = PixelOffset(8, 0);
    AssertTrue(frame[shifted] > 200, "positive horizontal scroll should move cell 0 right by one tile");
    AssertEqual((byte)0, frame[shifted + 1]);
    AssertEqual((byte)0, frame[shifted + 2]);
}

void VdpInterlaceDoubleModeIndexesHscrollByDisplayLine()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C87); // H40 mode with interlace double resolution.
    vdp.WriteControlPort(0x8B03); // Per-line horizontal scroll mode.
    vdp.WriteControlPort(0x8D02); // Horizontal scroll table at $0800.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8230); // Plane A table at $C000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteTallTile(vdp, tile: 1, topColor: 1, bottomColor: 1);
    WriteVramWordAt(vdp, 0xC000, 0x0001);
    WriteVramWordAt(vdp, 0xC040, 0x0001);
    WriteVramWordAt(vdp, 0x0800 + (8 * 4), 0x0008); // Display line 8.
    WriteVramWordAt(vdp, 0x0800 + (16 * 4), 0x0000); // Doubled source line 16 must not be used.

    byte[] frame = vdp.RenderFrameRgb();
    int lineEight = PixelOffset(0, 8);
    AssertEqual((byte)0, frame[lineEight]);
    AssertEqual((byte)0, frame[lineEight + 1]);
    AssertEqual((byte)0, frame[lineEight + 2]);
    int shifted = PixelOffset(8, 8);
    AssertTrue(frame[shifted] > 200, "interlace H-scroll should use display line index, not doubled source line");
}

void VdpFrameRendererUsesPerLineHscrollSnapshots()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x8B03); // Per-line horizontal scroll mode.
    vdp.WriteControlPort(0x8D02); // Horizontal scroll table at $0800.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8230); // Plane A table at $C000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteVramWordAt(vdp, 0xC000, 0x0001);
    WriteVramWordAt(vdp, 0xC040, 0x0001);
    WriteVramWordAt(vdp, 0x0800, 0x0000);
    WriteVramWordAt(vdp, 0x0800 + (8 * 4), 0x0008);

    vdp.BeginFrame(pal: false);
    vdp.StepScanline(0, pal: false);
    WriteVramWordAt(vdp, 0x0800, 0x0008);
    vdp.StepScanline(8, pal: false);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "line zero should use the H-scroll value captured before the mid-frame write");
    int lineEight = PixelOffset(0, 8);
    AssertEqual((byte)0, frame[lineEight]);
    int shifted = PixelOffset(8, 8);
    AssertTrue(frame[shifted] > 200, "later lines should use their own captured H-scroll values");
}

void VdpFrameRendererUsesPerLineRegisterSnapshots()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8230); // Plane A table at $C000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteCramColor(vdp, 2, 0x00E0);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteSolidTile(vdp, tile: 2, color: 2);
    WriteVramWordAt(vdp, 0xC000, 0x0001);
    WriteVramWordAt(vdp, 0xE000, 0x0002);
    WriteVramWordAt(vdp, 0xE040, 0x0002);

    vdp.BeginFrame(pal: false);
    vdp.StepScanline(0, pal: false);
    vdp.WriteControlPort(0x8238); // Plane A table at $E000 for later lines.
    vdp.StepScanline(8, pal: false);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "line zero should use the Plane A base captured before the mid-frame register write");
    AssertEqual((byte)0, frame[1]);
    int later = PixelOffset(0, 8);
    AssertTrue(frame[later + 1] > 200, "later lines should use their own captured Plane A base register");
    AssertEqual((byte)0, frame[later]);
    AssertEqual((byte)0x38, vdp.Registers[2]);
}

void VdpFrameRendererUsesPerLineCramSnapshots()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8230); // Plane A table at $C000.

    WriteCramColor(vdp, 1, 0x000E); // Red.
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteVramWordAt(vdp, 0xC000, 0x0001);
    WriteVramWordAt(vdp, 0xC040, 0x0001);

    vdp.BeginFrame(pal: false);
    vdp.StepScanline(0, pal: false);
    WriteCramColor(vdp, 1, 0x00E0); // Green after line 0 was captured.
    vdp.StepScanline(8, pal: false);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "line zero should use the CRAM color captured before the mid-frame write");
    AssertEqual((byte)0, frame[1]);
    int lineEight = PixelOffset(0, 8);
    AssertTrue(frame[lineEight + 1] > 200, "later lines should use their own captured CRAM colors");
    AssertEqual((byte)0, frame[lineEight]);
}

void VdpFrameRendererUsesPerLineVsramSnapshots()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8230); // Plane A table at $C000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteCramColor(vdp, 2, 0x00E0);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteSolidTile(vdp, tile: 2, color: 2);
    WriteVramWordAt(vdp, 0xC000, 0x0001);
    WriteVramWordAt(vdp, 0xC080, 0x0002); // Plane row 2.

    vdp.BeginFrame(pal: false);
    vdp.StepScanline(0, pal: false);
    WriteVsramWordAt(vdp, 0, 0x0008);
    vdp.StepScanline(8, pal: false);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "line zero should use the VSRAM value captured before the mid-frame write");
    int lineEight = PixelOffset(0, 8);
    AssertTrue(frame[lineEight + 1] > 200, "later lines should use the updated VSRAM snapshot");
    AssertEqual((byte)0, frame[lineEight]);
    AssertEqual((byte)0, frame[lineEight + 2]);
}

void VdpFrameRendererAppliesWindowPlane()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8200); // Plane A table at $0000.
    vdp.WriteControlPort(0x8304); // Window table at $1000.
    vdp.WriteControlPort(0x9102); // Window covers the left 16 pixels.
    vdp.WriteControlPort(0x9200);

    WriteCramColor(vdp, 1, 0x000E);
    WriteCramColor(vdp, 2, 0x0E00);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteSolidTile(vdp, tile: 2, color: 2);
    WriteVramWordAt(vdp, 0x0000, 0x0002); // Plane A is blue.
    WriteVramWordAt(vdp, 0x0002, 0x0002);
    WriteVramWordAt(vdp, 0x0004, 0x0002);
    WriteVramWordAt(vdp, 0x0006, 0x0002);
    WriteVramWordAt(vdp, 0x1000, 0x0001); // Window is red.
    WriteVramWordAt(vdp, 0x1002, 0x0001);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "window pixel should be red");
    int outside = 24 * 3;
    AssertTrue(frame[outside + 2] > 200, "outside the window should show plane A");
}

void VdpH40WindowUsesSixtyFourCellStride()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x8304); // Window table at $1000.
    vdp.WriteControlPort(0x9180); // Window covers the whole screen horizontally.
    vdp.WriteControlPort(0x9200); // Window covers the top of the screen vertically.

    WriteCramColor(vdp, 1, 0x000E); // Red.
    WriteCramColor(vdp, 2, 0x00E0); // Green.
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteSolidTile(vdp, tile: 2, color: 2);
    WriteVramWordAt(vdp, 0x1000, 0x0001);
    WriteVramWordAt(vdp, 0x1000 + (64 * 2), 0x0002);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "first H40 window row should read entry 0");
    int secondRow = PixelOffset(0, 8);
    AssertTrue(frame[secondRow + 1] > 200, "second H40 window row should read entry 64, not entry 40");
    AssertEqual((byte)0, frame[secondRow]);
    AssertEqual((byte)0, frame[secondRow + 2]);
}

  void VdpFrameRendererAppliesPriority()
  {
      Vdp vdp = new();
      vdp.WriteControlPort(0x8140); // Display enable.
      vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8200); // Plane A table at $0000.
    vdp.WriteControlPort(0x8401); // Plane B table at $2000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteCramColor(vdp, 2, 0x0E00);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteSolidTile(vdp, tile: 2, color: 2);
    WriteVramWordAt(vdp, 0x0000, 0x0001);
    WriteVramWordAt(vdp, 0x2000, 0x8002);

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[2] > 200, "high-priority plane B should cover low-priority plane A");
      AssertEqual((byte)0, frame[0]);
      AssertEqual((byte)0, frame[1]);
  }

  void VdpSpriteLinkPriorityFeedsLayerPriority()
  {
      Vdp vdp = new();
      vdp.WriteControlPort(0x8140); // Display enable.
      vdp.WriteControlPort(0x8C81); // H40 mode.
      vdp.WriteControlPort(0x9000); // 32x32 plane size.
      vdp.WriteControlPort(0x8200); // Plane A table at $0000.
      vdp.WriteControlPort(0x8502); // Sprite table at $0400.

      WriteCramColor(vdp, 1, 0x000E); // Red plane.
      WriteCramColor(vdp, 2, 0x00E0); // Green low-priority sprite.
      WriteCramColor(vdp, 3, 0x0E00); // Blue high-priority sprite.
      WriteSolidTile(vdp, tile: 1, color: 1);
      WriteSolidTile(vdp, tile: 2, color: 2);
      WriteSolidTile(vdp, tile: 3, color: 3);
      WriteVramWordAt(vdp, 0x0000, 0x8001); // High-priority Plane A should cover a low-priority sprite pixel.
      WriteSprite(vdp, 0x0400, y: 0, x: 0, tile: 2, link: 1);
      WriteSprite(vdp, 0x0408, y: 0, x: 0, tile: 3, link: 0);
      WriteVramWordAt(vdp, 0x0408 + 4, 0x8003); // Later sprite has high display priority but lower link priority.

      byte[] frame = vdp.RenderFrameRgb();
      AssertTrue(frame[0] > 200, "earlier low-priority sprites should occupy the sprite layer before layer priority is applied");
      AssertEqual((byte)0, frame[1]);
      AssertEqual((byte)0, frame[2]);
  }

void VdpFrameRendererAppliesShadowHighlight()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.WriteControlPort(0x8C89); // H40 mode with shadow/highlight enabled.
    vdp.WriteControlPort(0x9000); // 32x32 plane size.
    vdp.WriteControlPort(0x8230); // Plane A table at $C000.
    vdp.WriteControlPort(0x8407); // Plane B table at $E000.
    vdp.WriteControlPort(0x8568); // Sprite attribute table at $D000.

    WriteCramColor(vdp, 1, 0x000E);
    WriteCramColor(vdp, 46, 0x000E);
    WriteCramColor(vdp, 47, 0x000E);
    WriteSolidTile(vdp, tile: 1, color: 1);
    WriteSolidTile(vdp, tile: 2, color: 15);
    WriteVramWordAt(vdp, 0xC000, 0x0001); // Low-priority red is shadowed by the priority rule.

    byte[] frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 80 && frame[0] < 180, $"low-priority plane pixels should be shadowed in shadow/highlight mode, got RGB {frame[0]},{frame[1]},{frame[2]}");
    AssertEqual((byte)0, frame[1]);
    AssertEqual((byte)0, frame[2]);

    WriteVramWordAt(vdp, 0xE000, 0x0001); // Low-priority Plane B is red.
    WriteVramWordAt(vdp, 0xC000, 0x8000); // Transparent high-priority Plane A cancels automatic shadow.
    frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "transparent high-priority plane pixels should still affect shadow/highlight priority");
    AssertEqual((byte)0, frame[1]);
    AssertEqual((byte)0, frame[2]);

    WriteVramWordAt(vdp, 0xC000, 0x8001); // High-priority red.
    frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "high-priority plane pixels should render normally without an effect sprite");
    AssertEqual((byte)0, frame[1]);
    AssertEqual((byte)0, frame[2]);

    WriteSpriteWithSize(vdp, 0xD000, y: 0, x: 0, tile: 2, sizeLink: 0);
    WriteVramWordAt(vdp, 0xD004, 0x6002); // Palette 3, color 15 acts as a shadow sprite pixel.
    frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 200, "low-priority shadow sprite pixels should not affect high-priority plane pixels");
    AssertEqual((byte)0, frame[1]);
    AssertEqual((byte)0, frame[2]);

    WriteVramWordAt(vdp, 0xD004, 0xE002); // High-priority shadow sprite can affect high-priority planes.
    frame = vdp.RenderFrameRgb();
    AssertTrue(frame[0] > 80 && frame[0] < 180, "high-priority palette-3 color-15 sprite pixels should shadow the underlying plane");
    AssertEqual((byte)0, frame[1]);
    AssertEqual((byte)0, frame[2]);
}

  void VdpSpriteMaskPreservesHighPrioritySprites()
  {
      Vdp vdp = new();
      vdp.WriteControlPort(0x8140); // Display enable.
      vdp.WriteControlPort(0x8C81); // H40 mode.
    vdp.WriteControlPort(0x8502); // Sprite table at $0400.

    WriteCramColor(vdp, 1, 0x000E); // Red.
    WriteCramColor(vdp, 2, 0x00E0); // Green.
      WriteSolidTile(vdp, tile: 1, color: 1);
      WriteSolidTile(vdp, tile: 2, color: 2);
      WriteSprite(vdp, 0x0400, y: 0, x: 8, tile: 1, link: 1);
      WriteSprite(vdp, 0x0408, y: 0, x: -128, tile: 1, link: 2); // X=0 mask after one visible sprite.
      WriteSprite(vdp, 0x0410, y: 0, x: 16, tile: 1, link: 3); // Later low-priority sprite is masked.
      WriteSprite(vdp, 0x0418, y: 0, x: 24, tile: 2, link: 0);
      WriteVramWordAt(vdp, 0x0418 + 4, 0x8002); // Later high-priority sprite is preserved.

    byte[] frame = vdp.RenderFrameRgb();
    int beforeMask = PixelOffset(8, 0);
    AssertTrue(frame[beforeMask] > 200, "sprites before a mask should still render");
    int lowPriorityAfterMask = PixelOffset(16, 0);
    AssertEqual((byte)0, frame[lowPriorityAfterMask]);
      AssertEqual((byte)0, frame[lowPriorityAfterMask + 1]);
      AssertEqual((byte)0, frame[lowPriorityAfterMask + 2]);
      int highPriorityAfterMask = PixelOffset(24, 0);
      AssertTrue(frame[highPriorityAfterMask + 1] > 200, "high-priority sprites after a mask should still render");
      AssertEqual((byte)0, frame[highPriorityAfterMask]);
      AssertEqual((byte)0, frame[highPriorityAfterMask + 2]);
  }

  void VdpSpriteDotLimitClipsFinalSprite()
  {
      Vdp vdp = new();
      vdp.WriteControlPort(0x8140); // Display enable.
      vdp.WriteControlPort(0x8C81); // H40 mode.
      vdp.WriteControlPort(0x8502); // Sprite table at $0400.

      WriteCramColor(vdp, 1, 0x000E);
      WriteCramColor(vdp, 2, 0x00E0);
      WriteSolidTile(vdp, tile: 1, color: 1);
      WriteSolidTile(vdp, tile: 2, color: 2);
      WriteSolidTile(vdp, tile: 3, color: 2);
      WriteSolidTile(vdp, tile: 4, color: 2);
      WriteSolidTile(vdp, tile: 5, color: 2);

      for (int i = 0; i < 9; i++)
      {
          int link = i + 1;
          WriteSpriteWithSize(vdp, 0x0400 + (i * 8), y: 0, x: 320, tile: 1, sizeLink: (3 << 10) | link);
      }

      WriteSpriteWithSize(vdp, 0x0448, y: 0, x: 320, tile: 1, sizeLink: (1 << 10) | 10);
      WriteSpriteWithSize(vdp, 0x0450, y: 0, x: 32, tile: 2, sizeLink: (3 << 10));

      byte[] frame = vdp.RenderFrameRgb();
      int visibleStart = PixelOffset(32, 0);
      AssertTrue(frame[visibleStart + 1] > 200, "remaining sprite-dot budget should draw the start of the final sprite");
      int visibleEnd = PixelOffset(47, 0);
      AssertTrue(frame[visibleEnd + 1] > 200, "the final in-budget column should still render");
      int clipped = PixelOffset(48, 0);
      AssertEqual((byte)0, frame[clipped]);
      AssertEqual((byte)0, frame[clipped + 1]);
      AssertEqual((byte)0, frame[clipped + 2]);
  }

void VdpSpriteStatusFlags()
{
    Vdp collision = new();
    collision.WriteControlPort(0x8140); // Display enable.
    collision.WriteControlPort(0x8C81); // H40 mode.
    collision.WriteControlPort(0x8502); // Sprite table at $0400.
    WriteCramColor(collision, 1, 0x000E);
    WriteSolidTile(collision, tile: 1, color: 1);
    WriteSprite(collision, 0x0400, y: 0, x: 0, tile: 1, link: 1);
    WriteSprite(collision, 0x0408, y: 0, x: 0, tile: 1, link: 0);

    _ = collision.RenderFrameRgb();
    AssertTrue((collision.Status & 0x0020) != 0, "overlapping opaque sprites should set collision");

    Vdp overflow = new();
    overflow.WriteControlPort(0x8140); // Display enable.
    overflow.WriteControlPort(0x8C81); // H40 mode.
    overflow.WriteControlPort(0x8502); // Sprite table at $0400.
    WriteCramColor(overflow, 1, 0x000E);
    WriteSolidTile(overflow, tile: 1, color: 1);
    for (int i = 0; i < 21; i++)
    {
        WriteSprite(overflow, 0x0400 + (i * 8), y: 0, x: i * 8, tile: 1, link: i == 20 ? 0 : i + 1);
    }

    byte[] overflowFrame = overflow.RenderFrameRgb();
    AssertTrue((overflow.Status & 0x0040) != 0, "more than twenty H40 sprites on one line should set overflow");
    int twentieth = PixelOffset(19 * 8, 0);
    AssertTrue(overflowFrame[twentieth] > 200, "the twentieth H40 sprite on a line should still render");
    int twentyFirst = PixelOffset(20 * 8, 0);
    AssertEqual((byte)0, overflowFrame[twentyFirst]);
    AssertEqual((byte)0, overflowFrame[twentyFirst + 1]);
    AssertEqual((byte)0, overflowFrame[twentyFirst + 2]);
}

void VdpDirectColorDmaCaptureRendersFrame()
{
    Vdp vdp = new();
    vdp.WriteControlPort(0x8140); // Display enable.
    vdp.BeginDmaMemoryCopy(new Vdp.DmaRequest(0, Vdp.ScreenHeight * 160, 0x23, 0, 0));
    for (int y = 0; y < Vdp.ScreenHeight; y++)
    {
        for (int x = 0; x < 160; x++)
        {
            ushort color = x < 80 ? (ushort)0x000E : (ushort)0x0E00;
            vdp.WriteDmaWord(color);
        }
    }

    byte[] frame = vdp.RenderFrameRgb();
    AssertEqual("direct-color-dma", vdp.LastRenderMode);
    AssertTrue(frame[0] > 200, "left direct-color pixel should be red");
    int right = (200 * 3) + 2;
    AssertTrue(frame[right] > 200, "right direct-color pixel should be blue");
}

byte[] CreateRom()
{
    return new byte[512 * 1024];
}

void DeclareSaveRam(byte[] data, uint start = 0x0020_0000, uint end = 0x0020_FFFF, byte lanes = 0x60)
{
    WriteAscii(data, 0x1B0, "RA");
    data[0x1B2] = 0xF8;
    data[0x1B3] = lanes;
    WriteLong(data, 0x1B4, start);
    WriteLong(data, 0x1B8, end);
}

void DeclareEeprom(byte[] data)
{
    WriteAscii(data, 0x1B0, "RA");
    data[0x1B2] = 0xE8;
    data[0x1B3] = 0x40;
    WriteLong(data, 0x1B4, 0x0020_0001);
    WriteLong(data, 0x1B8, 0x0020_0001);
}

void EepromWriteBytes(MegaDrive machine, EepromPins pins, byte command, byte[] addressBytes, byte[] data)
{
    EepromStart(machine, pins);
    EepromSendByte(machine, pins, command);
    EepromAckClock(machine, pins);
    foreach (byte addressByte in addressBytes)
    {
        EepromSendByte(machine, pins, addressByte);
        EepromAckClock(machine, pins);
    }

    foreach (byte value in data)
    {
        EepromSendByte(machine, pins, value);
        EepromAckClock(machine, pins);
    }

    EepromStop(machine, pins);
}

byte EepromReadAt(MegaDrive machine, EepromPins pins, byte writeCommand, byte readCommand, byte[] addressBytes)
{
    EepromStart(machine, pins);
    EepromSendByte(machine, pins, writeCommand);
    EepromAckClock(machine, pins);
    foreach (byte addressByte in addressBytes)
    {
        EepromSendByte(machine, pins, addressByte);
        EepromAckClock(machine, pins);
    }

    EepromStart(machine, pins);
    EepromSendByte(machine, pins, readCommand);
    EepromAckClock(machine, pins);
    byte value = EepromReadByte(machine, pins, ack: false);
    EepromStop(machine, pins);
    return value;
}

void EepromStart(MegaDrive machine, EepromPins pins)
{
    EepromWriteLines(machine, pins, sda: true, scl: true);
    EepromWriteLines(machine, pins, sda: false, scl: true);
    EepromWriteLines(machine, pins, sda: false, scl: false);
}

void EepromStop(MegaDrive machine, EepromPins pins)
{
    EepromWriteLines(machine, pins, sda: false, scl: false);
    EepromWriteLines(machine, pins, sda: false, scl: true);
    EepromWriteLines(machine, pins, sda: true, scl: true);
}

void EepromSendByte(MegaDrive machine, EepromPins pins, byte value)
{
    for (int bit = 7; bit >= 0; bit--)
    {
        bool sda = ((value >> bit) & 1) != 0;
        EepromWriteLines(machine, pins, sda, scl: false);
        EepromWriteLines(machine, pins, sda, scl: true);
        EepromWriteLines(machine, pins, sda, scl: false);
    }
}

void EepromAckClock(MegaDrive machine, EepromPins pins)
{
    EepromWriteLines(machine, pins, sda: true, scl: false);
    EepromWriteLines(machine, pins, sda: true, scl: true);
    AssertEqual((byte)0, (byte)(machine.Bus.ReadByte(pins.SdaOutAddress) & (1 << pins.SdaOutBit)));
    EepromWriteLines(machine, pins, sda: true, scl: false);
}

byte EepromReadByte(MegaDrive machine, EepromPins pins, bool ack)
{
    byte value = 0;
    for (int bit = 7; bit >= 0; bit--)
    {
        EepromWriteLines(machine, pins, sda: true, scl: false);
        EepromWriteLines(machine, pins, sda: true, scl: true);
        value = (byte)((value << 1) | ((machine.Bus.ReadByte(pins.SdaOutAddress) >> pins.SdaOutBit) & 0x01));
        EepromWriteLines(machine, pins, sda: true, scl: false);
    }

    EepromWriteLines(machine, pins, sda: ack ? false : true, scl: false);
    EepromWriteLines(machine, pins, sda: ack ? false : true, scl: true);
    EepromWriteLines(machine, pins, sda: ack ? false : true, scl: false);
    return value;
}

void EepromWriteLines(MegaDrive machine, EepromPins pins, bool sda, bool scl)
{
    if (pins.WordAccess)
    {
        if (pins.SclAddress + 1 != pins.SdaInAddress)
        {
            throw new InvalidOperationException("word EEPROM helper expects SCL on the high byte and SDA on the low byte");
        }

        ushort value = (ushort)((scl ? 1 << (pins.SclBit + 8) : 0) | (sda ? 1 << pins.SdaInBit : 0));
        machine.Bus.WriteWord(pins.SclAddress, value);
        return;
    }

    if (pins.SdaInAddress == pins.SclAddress)
    {
        int value = (sda ? 1 << pins.SdaInBit : 0) | (scl ? 1 << pins.SclBit : 0);
        machine.Bus.WriteByte(pins.SdaInAddress, (byte)value);
        return;
    }

    machine.Bus.WriteByte(pins.SdaInAddress, (byte)(sda ? 1 << pins.SdaInBit : 0));
    machine.Bus.WriteByte(pins.SclAddress, (byte)(scl ? 1 << pins.SclBit : 0));
}

void WriteAscii(byte[] data, int offset, string text)
{
    for (int i = 0; i < text.Length; i++)
    {
        data[offset + i] = (byte)text[i];
    }
}

void WriteWord(byte[] data, int offset, ushort value)
{
    data[offset] = (byte)(value >> 8);
    data[offset + 1] = (byte)value;
}

void WriteLong(byte[] data, int offset, uint value)
{
    WriteWord(data, offset, (ushort)(value >> 16));
    WriteWord(data, offset + 2, (ushort)value);
}

void EmitWord(byte[] data, ref int offset, ushort value)
{
    WriteWord(data, offset, value);
    offset += 2;
}

void EmitLong(byte[] data, ref int offset, uint value)
{
    WriteLong(data, offset, value);
    offset += 4;
}

void ConfigureDma(Vdp vdp, int lengthWords, uint sourceAddress, byte mode)
{
    int sourceHigh = (int)((sourceAddress >> 17) & 0x7F);
    int modeAndSource = mode <= 1 ? sourceHigh : ((int)mode << 6) | (sourceHigh & 0x3F);
    vdp.WriteControlPort((ushort)(0x9300 | (lengthWords & 0xFF)));
    vdp.WriteControlPort((ushort)(0x9400 | ((lengthWords >> 8) & 0xFF)));
    vdp.WriteControlPort((ushort)(0x9500 | ((sourceAddress >> 1) & 0xFF)));
    vdp.WriteControlPort((ushort)(0x9600 | ((sourceAddress >> 9) & 0xFF)));
    vdp.WriteControlPort((ushort)(0x9700 | modeAndSource));
    vdp.WriteControlPort(0x8110); // DMA enable.
    vdp.WriteControlPort(0x8F02); // Auto-increment by one word.
}

void WriteCramColor(Vdp vdp, int index, ushort value)
{
    vdp.WriteControlPort((ushort)(0xC000 | ((index * 2) & 0x3FFF)));
    vdp.WriteControlPort(0x0000);
    vdp.WriteDataPort(value);
}

void WriteVramWordAt(Vdp vdp, int address, ushort value)
{
    vdp.WriteControlPort((ushort)(0x4000 | (address & 0x3FFF)));
    vdp.WriteControlPort((ushort)((address >> 14) & 0x0003));
    vdp.WriteDataPort(value);
}

void WriteVsramWordAt(Vdp vdp, int address, ushort value)
{
    vdp.WriteControlPort((ushort)(0x4000 | (address & 0x3FFF)));
    vdp.WriteControlPort((ushort)(0x0010 | ((address >> 14) & 0x0003)));
    vdp.WriteDataPort(value);
}

void WriteSolidTile(Vdp vdp, int tile, int color)
{
    ushort word = (ushort)((color << 12) | (color << 8) | (color << 4) | color);
    int address = tile * 32;
    for (int i = 0; i < 16; i++)
    {
        WriteVramWordAt(vdp, address + (i * 2), word);
    }
}

void WriteTallTile(Vdp vdp, int tile, int topColor, int bottomColor)
{
    int address = tile * 64;
    for (int y = 0; y < 16; y++)
    {
        int color = y < 8 ? topColor : bottomColor;
        ushort word = (ushort)((color << 12) | (color << 8) | (color << 4) | color);
        WriteVramWordAt(vdp, address + (y * 4), word);
        WriteVramWordAt(vdp, address + (y * 4) + 2, word);
    }
}

void WriteSprite(Vdp vdp, int address, int y, int x, int tile, int link)
{
    WriteSpriteWithSize(vdp, address, y, x, tile, link & 0x7F);
}

void WriteSpriteWithSize(Vdp vdp, int address, int y, int x, int tile, int sizeLink)
{
    WriteVramWordAt(vdp, address, (ushort)(y + 128));
    WriteVramWordAt(vdp, address + 2, (ushort)sizeLink);
    WriteVramWordAt(vdp, address + 4, (ushort)(tile & 0x07FF));
    WriteVramWordAt(vdp, address + 6, (ushort)(x + 128));
}

void WriteInterlaceSpriteWithSize(Vdp vdp, int address, int y, int x, int tile, int sizeLink)
{
    WriteVramWordAt(vdp, address, (ushort)(y + 256));
    WriteVramWordAt(vdp, address + 2, (ushort)sizeLink);
    WriteVramWordAt(vdp, address + 4, (ushort)(tile & 0x07FF));
    WriteVramWordAt(vdp, address + 6, (ushort)(x + 128));
}

int PixelOffset(int x, int y)
{
    return ((y * Vdp.ScreenWidth) + x) * 3;
}

void AssertEqual<T>(T expected, T actual, [CallerLineNumber] int line = 0)
    where T : IEquatable<T>
{
    if (!expected.Equals(actual))
    {
        throw new InvalidOperationException($"line {line}: expected {expected}, got {actual}");
    }
}

void AssertTrue(bool condition, string message, [CallerLineNumber] int line = 0)
{
    if (!condition)
    {
        throw new InvalidOperationException($"line {line}: {message}");
    }
}

static void SetSh2Property(Sh2Cpu cpu, string propertyName, uint value)
{
    typeof(Sh2Cpu).GetProperty(propertyName)!.SetValue(cpu, value);
}

static void SetSh2BoolProperty(Sh2Cpu cpu, string propertyName, bool value)
{
    typeof(Sh2Cpu).GetProperty(propertyName)!.SetValue(cpu, value);
}

static byte ReadSh2ByteForTest(ThirtyTwoXDevice target, uint address, int cpuIndex = 0)
{
    System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ReadSh2Byte helper was not found");
    return (byte)method.Invoke(target, [address, cpuIndex])!;
}

static ushort ReadSh2WordForTest(ThirtyTwoXDevice target, uint address, int cpuIndex = 0)
{
    System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Word", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ReadSh2Word helper was not found");
    return (ushort)method.Invoke(target, [address, cpuIndex])!;
}

static uint ReadSh2LongForTest(ThirtyTwoXDevice target, uint address, int cpuIndex = 0)
{
    System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("ReadSh2Long", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ReadSh2Long helper was not found");
    return (uint)method.Invoke(target, [address, cpuIndex])!;
}

static void WriteSh2ByteForTest(ThirtyTwoXDevice target, uint address, byte value, int cpuIndex = 0)
{
    System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Byte", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("WriteSh2Byte helper was not found");
    method.Invoke(target, [address, value, cpuIndex]);
}

static void WriteSh2WordForTest(ThirtyTwoXDevice target, uint address, ushort value, int cpuIndex = 0)
{
    System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Word", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("WriteSh2Word helper was not found");
    method.Invoke(target, [address, value, cpuIndex]);
}

static void WriteSh2LongForTest(ThirtyTwoXDevice target, uint address, uint value, int cpuIndex = 0)
{
    System.Reflection.MethodInfo method = typeof(ThirtyTwoXDevice).GetMethod("WriteSh2Long", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("WriteSh2Long helper was not found");
    method.Invoke(target, [address, value, cpuIndex]);
}

readonly record struct EepromPins(uint SdaInAddress, int SdaInBit, uint SdaOutAddress, int SdaOutBit, uint SclAddress, int SclBit, bool WordAccess = false);

sealed class SyntheticSh2Bus : ISh2Bus, ISh2PeekBus
{
    private readonly Dictionary<uint, byte> _memory = [];

    public byte ReadByte(uint address) => _memory.TryGetValue(address, out byte value) ? value : (byte)0x00;

    public ushort ReadWord(uint address) => (ushort)((ReadByte(address) << 8) | ReadByte(address + 1));

    public uint ReadLong(uint address) => (uint)((ReadWord(address) << 16) | ReadWord(address + 2));

    public void WriteByte(uint address, byte value) => _memory[address] = value;

    public void WriteWord(uint address, ushort value)
    {
        WriteByte(address, (byte)(value >> 8));
        WriteByte(address + 1, (byte)value);
    }

    public void WriteLong(uint address, uint value)
    {
        WriteWord(address, (ushort)(value >> 16));
        WriteWord(address + 2, (ushort)value);
    }

    public void WriteInstructionWord(uint address, ushort value) => WriteWord(address, value);

    public bool TryPeekByte(uint address, out byte value)
    {
        value = ReadByte(address);
        return true;
    }

    public bool TryPeekWord(uint address, out ushort value)
    {
        value = ReadWord(address);
        return true;
    }

    public uint? TryReadLong(uint address) => ReadLong(address);

    public ushort? TryReadWord(uint address) => ReadWord(address);

    public byte? TryReadByte(uint address) => ReadByte(address);

    public bool TryWriteWord(uint address, ushort value)
    {
        WriteWord(address, value);
        return true;
    }

    public bool TryWriteLong(uint address, uint value)
    {
        WriteLong(address, value);
        return true;
    }

    public bool TryWriteByte(uint address, byte value)
    {
        WriteByte(address, value);
        return true;
    }
}
