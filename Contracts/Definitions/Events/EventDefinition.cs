﻿// <copyright file="EventDefinition.cs" company="Tom Luppi">
//     Copyright (c) Tom Luppi.  All rights reserved.
// </copyright>

namespace LegendsGenerator.Contracts.Definitions.Events
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Serialization;

    using LegendsGenerator.Contracts.Compiler;
    using LegendsGenerator.Contracts.Definitions.Validation;

    /// <summary>
    /// The definition of a single event.
    /// </summary>
    public partial class EventDefinition : BaseDefinition, ITopLevelDefinition
    {
        /// <inheritdoc/>
        [JsonIgnore]
        public string SourceFile { get; set; } = UnsetString;

        /// <summary>
        /// Gets or sets the defintion name, if not set the definition name will be the event description.
        /// </summary>
        [ControlsDefinitionName]
        public string? DefinitionName { get; set; }

        /// <inheritdoc/>
        [JsonIgnore]
        string ITopLevelDefinition.DefinitionName
        {
            get
            {
                if (string.IsNullOrEmpty(this.DefinitionName))
                {
                    return this.Description;
                }
                else
                {
                    return this.DefinitionName;
                }
            }

            set
            {
                this.DefinitionName = value;
            }
        }

        /// <summary>
        /// Gets or sets the event Condition, from one to one hundred.
        /// </summary>
        [Compiled(typeof(int), "Subject")]
        public string Chance { get; set; } = "100";

        /// <summary>
        /// Gets or sets the subject of this event.
        /// </summary>
        public SubjectDefinition Subject { get; set; } = new SubjectDefinition();

        /// <summary>
        /// Gets or sets the objects of this event.
        /// </summary>
        public Dictionary<string, ObjectDefinition> Objects { get; set; } = new Dictionary<string, ObjectDefinition>();

        /// <summary>
        /// Gets or sets the description of this event.
        /// </summary>
        [Compiled(typeof(string), "Subject", AsFormattedText = true)]
        [ControlsDefinitionName]
        public string Description { get; set; } = UnsetString;

        /// <summary>
        /// Gets or sets the results of this event.
        /// </summary>
        public List<EventResultDefinition> Results { get; set; } = new List<EventResultDefinition>();

        /// <summary>
        /// Gets additional variable names for the Description method.
        /// </summary>
        /// <returns>The list of additional parameters.</returns>
        public IList<string> AdditionalParametersForClass()
        {
            return this.Objects?.Select(x => x.Key).ToList() ?? new List<string>();
        }
    }
}
