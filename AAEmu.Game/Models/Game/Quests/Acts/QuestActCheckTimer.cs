﻿using System;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Tasks.Quests;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActCheckTimer : QuestActTemplate
    {
        public int LimitTime { get; set; }
        public bool ForceChangeComponent { get; set; }
        public uint NextComponent { get; set; }
        public bool PlaySkill { get; set; }
        public uint SkillId { get; set; }
        public bool CheckBuff { get; set; }
        public uint BuffId { get; set; }
        public bool SustainBuff { get; set; }
        public uint TimerNpcId { get; set; }
        public bool IsSkillPlayer { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Warn("QuestActCheckTimer");
            // TODO add what to do with timer
            // TODO настройка и старт таймера ограничения времени на квест
            QuestManager.Instance.QuestTimeoutTask.Add(quest.TemplateId, new QuestTimeoutTask(character, quest.TemplateId));
            TaskManager.Instance.Schedule(QuestManager.Instance.QuestTimeoutTask[quest.TemplateId], TimeSpan.FromMilliseconds(objective));
            character.SendMessage("[Quest] {0}, quest {1} will end in {2} seconds.", character.Name, quest.TemplateId, objective / 1000);

            return true;
        }
    }
}
