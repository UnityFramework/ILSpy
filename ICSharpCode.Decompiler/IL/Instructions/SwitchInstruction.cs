// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System.Diagnostics;
using System.Linq;
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.IL
{
	/// <summary>
	/// Generalization of IL switch-case: like a VB switch over integers, this instruction
	/// supports integer value ranges as labels.
	/// 
	/// The section labels are using 'long' as integer type.
	/// If the Value instruction produces StackType.I4 or I, the value is implicitly sign-extended to I8.
	/// </summary>
	partial class SwitchInstruction
	{
		public static readonly SlotInfo ValueSlot = new SlotInfo("Value", canInlineInto: true);
		public static readonly SlotInfo DefaultBodySlot = new SlotInfo("DefaultBody");
		public static readonly SlotInfo SectionSlot = new SlotInfo("Section", isCollection: true);
		
		public SwitchInstruction(ILInstruction value)
			: base(OpCode.SwitchInstruction)
		{
			this.Value = value;
			this.DefaultBody = new Nop();
			this.Sections = new InstructionCollection<SwitchSection>(this, 2);
		}
		
		ILInstruction value;
		public ILInstruction Value {
			get { return this.value; }
			set {
				ValidateChild(value);
				SetChildInstruction(ref this.value, value, 0);
			}
		}
		
		ILInstruction defaultBody;

		public ILInstruction DefaultBody {
			get { return this.defaultBody; }
			set {
				ValidateChild(value);
				SetChildInstruction(ref this.defaultBody, value, 1);
			}
		}

		public readonly InstructionCollection<SwitchSection> Sections;

		protected override InstructionFlags ComputeFlags()
		{
			var sectionFlags = defaultBody.Flags;
			foreach (var section in Sections) {
				sectionFlags = SemanticHelper.CombineBranches(sectionFlags, section.Flags);
			}
			return value.Flags | InstructionFlags.ControlFlow | sectionFlags;
		}
		
		public override InstructionFlags DirectFlags {
			get {
				return InstructionFlags.ControlFlow;
			}
		}
		
		public override void WriteTo(ITextOutput output)
		{
			output.Write("switch (");
			value.WriteTo(output);
			output.Write(") ");
			output.MarkFoldStart("{...}");
			output.WriteLine("{");
			output.Indent();
			output.Write("default: ");
			defaultBody.WriteTo(output);
			output.WriteLine();
			foreach (var section in this.Sections) {
				section.WriteTo(output);
				output.WriteLine();
			}
			output.Unindent();
			output.Write('}');
			output.MarkFoldEnd();
		}
		
		protected override int GetChildCount()
		{
			return 2 + Sections.Count;
		}
		
		protected override ILInstruction GetChild(int index)
		{
			if (index == 0)
				return value;
			else if (index == 1)
				return defaultBody;
			return Sections[index - 2];
		}
		
		protected override void SetChild(int index, ILInstruction value)
		{
			if (index == 0)
				Value = value;
			else if (index == 1)
				DefaultBody = value;
			else
				Sections[index - 2] = (SwitchSection)value;
		}
		
		protected override SlotInfo GetChildSlot(int index)
		{
			if (index == 0)
				return ValueSlot;
			else if (index == 1)
				return DefaultBodySlot;
			return SectionSlot;
		}
		
		public override ILInstruction Clone()
		{
			var clone = new SwitchInstruction(value.Clone());
			clone.ILRange = this.ILRange;
			clone.Value = value.Clone();
			this.DefaultBody = defaultBody.Clone();
			clone.Sections.AddRange(this.Sections.Select(h => (SwitchSection)h.Clone()));
			return clone;
		}
		
		internal override void CheckInvariant(ILPhase phase)
		{
			base.CheckInvariant(phase);
			LongSet sets = LongSet.Empty;
			foreach (var section in Sections) {
				Debug.Assert(!section.Labels.IsEmpty);
				Debug.Assert(!section.Labels.Overlaps(sets));
				sets = sets.UnionWith(section.Labels);
			}
		}
	}
	
	partial class SwitchSection
	{
		public SwitchSection()
			: base(OpCode.SwitchSection)
		{
			this.Labels = LongSet.Empty;
		}

		public LongSet Labels { get; set; }
		
		protected override InstructionFlags ComputeFlags()
		{
			return body.Flags;
		}
		
		public override InstructionFlags DirectFlags {
			get {
				return InstructionFlags.None;
			}
		}
		
		public override void WriteTo(ITextOutput output)
		{
			output.Write("case ");
			output.Write(Labels.ToString());
			output.Write(": ");
			
			body.WriteTo(output);
		}
	}
}
