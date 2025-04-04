using System;
using System.Collections.Generic;
using Werewolves.Core.Resources;

namespace Werewolves.Core
{
    public class ModeratorInstruction
    {
        public string InstructionText { get; set; } = string.Empty;
        public ExpectedInputType ExpectedInputType { get; set; } = ExpectedInputType.None;
        public List<Guid>? SelectablePlayerIds { get; set; }
        public List<RoleType>? SelectableRoleTypes { get; set; }
        public List<string>? SelectableOptions { get; set; }
        public bool RequiresConfirmation { get; set; }

        /// <summary>
        /// Represents an empty or default instruction.
        /// </summary>
        public static ModeratorInstruction None => new() { InstructionText = GameStrings.Instruction_NoInstructionPending };

        // Constructor might be useful
        public ModeratorInstruction(string text = "", ExpectedInputType type = ExpectedInputType.None)
        {
            InstructionText = !string.IsNullOrEmpty(text) ? text : GameStrings.Instruction_NoInstructionPending;
            ExpectedInputType = type;
        }
    }
} 