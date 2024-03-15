﻿using System;
using System.Threading;

using AAEmu.Game.Core.Packets;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects;

public class SpecialEffect : EffectTemplate
{
    public SpecialType SpecialEffectTypeId { get; set; }
    public int Value1 { get; set; }
    public int Value2 { get; set; }
    public int Value3 { get; set; }
    public int Value4 { get; set; }

    public override bool OnActionTime => false;

    public override void Apply(BaseUnit caster, SkillCaster casterObj, BaseUnit target, SkillCastTarget targetObj,
        CastAction castObj, EffectSource source, SkillObject skillObject, DateTime time,
        CompressedGamePackets packetBuilder = null)
    {
        if (source == null) return;

        Logger.ConditionalTrace("SpecialEffect, Special: {0}, Value1: {1}, Value2: {2}, Value3: {3}, Value4: {4}", SpecialEffectTypeId, Value1, Value2, Value3, Value4);

        var classType = Type.GetType("AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects." + SpecialEffectTypeId);
        if (classType == null)
        {
            Logger.Warn("Unknown special effect: {0}", SpecialEffectTypeId);
            return;
        }

        var action = (SpecialEffectAction)Activator.CreateInstance(classType);
        if (source.Skill?.Template.EffectRepeatCount > 1)
        {
            for (var i = 0; i < source.Skill.Template.EffectRepeatCount; i++)
            {
                action?.Execute(caster, casterObj, target, targetObj, castObj, source.Skill, skillObject, time, Value1, Value2, Value3, Value4);
                Thread.Sleep(TimeSpan.FromMilliseconds(source.Skill.Template.EffectRepeatTick));
            }
        }
        else
        {
            action?.Execute(caster, casterObj, target, targetObj, castObj, source.Skill, skillObject, time, Value1, Value2, Value3, Value4);
        }
    }
}
