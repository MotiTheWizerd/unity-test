using System;
using System.Collections.Generic;
using Forge.Agent;
using Forge.Agent.Tools;
using Forge.Editing.Commands;
using UnityEngine;

namespace Forge.EditorApp.Agent
{
    /// <summary>
    /// The agent's eyes as a command: a picture of the world, as the user sees it or of a ground point from a chosen
    /// side, distance and angle, so the agent can check what it built before the user reviews it.
    /// </summary>
    public sealed class LookCommand : EditorCommand<LookCommand.Arguments, LookCommand.Result>
    {
        const float DefaultDistance = 40f;
        const float DefaultPitch = 35f;
        // Eye height above the ground point when the ground there is unknown (no terrain).
        const float NoGround = 0f;

        readonly AgentEyes _eyes;

        public LookCommand(AgentEyes eyes)
            : base("look",
                "Takes a picture of the world and shows it to you. With no arguments it shows exactly what the user sees " +
                "(the editor's view, without its panels). With x and z it looks at that ground point from a camera standing " +
                "distance meters away, facing heading (0 north, 90 east, 180 south, 270 west; the user's heading when left " +
                "out) and looking down by pitch degrees (90 is straight down, a plan view). The objects of the build waiting " +
                "for review are highlighted. Use it after building to check the result (gaps in a fence, objects floating " +
                "or sunk, overlaps, how it looks from where the user stands) and fix what's wrong before you finish. " +
                "Land far from the user's view is drawn coarser and may lack its smaller objects.")
        {
            _eyes = eyes ?? throw new ArgumentNullException(nameof(eyes));
        }

        public sealed class Arguments
        {
            [Param("East-west position of the ground point to look at, in meters. Give it with z; leave both out to see the user's view.", Optional = true)]
            public float? X { get; set; }

            [Param("North-south position of the ground point to look at, in meters. Give it with x.", Optional = true)]
            public float? Z { get; set; }

            [Param("How far the camera stands from the point, in meters. Default 40.", Optional = true, Minimum = 3, Maximum = 500)]
            public float? Distance { get; set; }

            [Param("Compass direction the camera faces, in degrees: 0 north, 90 east, 180 south, 270 west. Default: the way the user's camera faces.", Optional = true, Minimum = 0, Maximum = 360)]
            public float? Heading { get; set; }

            [Param("How far the camera looks down, in degrees: 90 is straight down. Default 35.", Optional = true, Minimum = 5, Maximum = 90)]
            public float? Pitch { get; set; }
        }

        public sealed class Result : IPictureResult
        {
            internal Result(string summary, Point camera, Point lookingAt, float heading, float pitch, AgentImage picture)
            {
                Summary = summary;
                Camera = camera;
                LookingAt = lookingAt;
                Heading = heading;
                Pitch = pitch;
                Pictures = new[] { picture };
            }

            public string Summary { get; }

            /// <summary>Where the camera stood.</summary>
            public Point Camera { get; }

            /// <summary>The ground point looked at; null for the user's own view.</summary>
            public Point LookingAt { get; }

            public float Heading { get; }

            public float Pitch { get; }

            public IReadOnlyList<AgentImage> Pictures { get; }
        }

        /// <summary>A world position, in meters.</summary>
        public sealed class Point
        {
            internal Point(Vector3 at)
            {
                X = Round(at.x);
                Y = Round(at.y);
                Z = Round(at.z);
            }

            public float X { get; }

            public float Y { get; }

            public float Z { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            if (arguments.X.HasValue != arguments.Z.HasValue)
                throw new CommandException("Give x and z together (the ground point to look at), or neither for the user's view.");

            if (!arguments.X.HasValue)
            {
                if (arguments.Distance.HasValue || arguments.Heading.HasValue || arguments.Pitch.HasValue)
                    throw new CommandException("Distance, heading and pitch need a ground point: give x and z too, or nothing for the user's view.");

                var position = _eyes.ViewPosition;
                return new Result("Looked at the user's view.", new Point(position), null, Round(_eyes.ViewHeading), Round(_eyes.ViewPitch), _eyes.View());
            }

            var x = arguments.X.Value;
            var z = arguments.Z.Value;
            var ground = _eyes.GroundHeight(x, z);
            var target = new Vector3(x, ground ?? NoGround, z);
            var heading = Mathf.Repeat(arguments.Heading ?? _eyes.ViewHeading, 360f);
            var pitch = arguments.Pitch ?? DefaultPitch;
            var distance = arguments.Distance ?? DefaultDistance;
            var eye = target - Quaternion.Euler(pitch, heading, 0f) * Vector3.forward * distance;
            var picture = _eyes.From(eye, heading, pitch);

            var summary = FormattableString.Invariant($"Looked at ({x:0.#}, {z:0.#}) from {distance:0.#} m, facing {Compass(heading)}.");
            if (!ground.HasValue)
                summary += " There is no terrain there.";
            return new Result(summary, new Point(eye), new Point(target), Round(heading), Round(pitch), picture);
        }

        static string Compass(float heading)
        {
            string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return names[Mathf.RoundToInt(heading / 45f) % names.Length];
        }

        static float Round(float value) => Mathf.Round(value * 10f) / 10f;
    }
}
