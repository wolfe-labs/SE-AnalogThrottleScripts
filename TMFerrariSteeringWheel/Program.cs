using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;
using IngameScript.WolfeLabs.AnalogThrottleAPI;
using System.Runtime.CompilerServices;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // My steering wheel has a slight offset to it, so if you can figure out what it sits at, you can change this
        static readonly float YAW_OFFSET = 0.4999924f;

        //Rover Class ------------------------------------------------------------------------------------------------------
        // -- Rover Class with stats for driving

        public class RoverController
        {
            public RoverController ()
            {
                acceleration = 0.0f;
                turnRatio = 0.0f;
                isBreaking = false;
                Gear = 0;
                breakStrength = 0.1f;
                wheels = new List<IMyMotorSuspension>();
            }

            public int Gear { get; set; }
            public bool isBreaking { get; set; }
            public float acceleration { get; set; }
            public float turnRatio { get; set; }
            public float breakStrength { get; set; }

            public List<IMyMotorSuspension> wheels { get; set; }
            public IMyShipController MainControl { get; set; }

            public override string ToString ()
            {
                return String.Join("\n", new string[] {
            "Acceleration: " + this.acceleration.ToString(),
            "TurnRatio: " + this.turnRatio.ToString(),
            "Breaks On: " + this.isBreaking.ToString(),
            "Break Strength: " + this.breakStrength.ToString()
        });
            }

            public void UpdateStats (string argument)
            {
                ControllerInputCollection inputs = ControllerInputCollection.FromString(argument);

                foreach (ControllerInput input in inputs) {
                    switch (input.Axis) {
                        case "Y":
                            if (input.AnalogValue < 0.95f)
                                this.isBreaking = true;
                            else
                                this.isBreaking = false;
                            break;
                        case "RZ":
                            this.acceleration = (float)System.Math.Round(input.AnalogValue * -1 + 1, 2);
                            break;
                        case "X":
                            this.turnRatio = (float)System.Math.Round((input.AnalogValue - 0.4999924f) * 2, 2);
                            break;

                    }
                }
            }

            private Vector3D GetAverageWheelPosition ()
            {
                var total = Vector3D.Zero;

                foreach (IMyMotorSuspension wheel in this.wheels) {
                    total += wheel.GetPosition();
                    wheel.PropulsionOverride = 0f;
                    wheel.SteeringOverride = 0f;
                }
                return total / this.wheels.Count;
            }

            public void ControlVehicle ()
            {
                var avgWheelPosition = GetAverageWheelPosition();
                bool brakes = this.MainControl.MoveIndicator.Y > 0 || this.MainControl.HandBrake;
                var velocity = Vector3D.TransformNormal(this.MainControl.GetShipVelocities().LinearVelocity, MatrixD.Transpose(this.MainControl.WorldMatrix)) * this.breakStrength;

                foreach (var wheel in this.wheels) {
                    var Propulsion = 0f;
                    var YawMultiplier = Math.Sign(Math.Round(Vector3D.Dot(wheel.WorldMatrix.Forward, this.MainControl.WorldMatrix.Up), 2)) * Math.Sign(Vector3D.Dot(wheel.GetPosition() - avgWheelPosition, this.MainControl.WorldMatrix.Forward));
                    var ForwardMultiplier = -Math.Sign(Math.Round(Vector3D.Dot(wheel.WorldMatrix.Up, this.MainControl.WorldMatrix.Right), 2));
                    var steerValue = YawMultiplier * this.turnRatio;
                    var Power = wheel.Power * 0.01f;

                    if (this.isBreaking) {
                        Propulsion = ForwardMultiplier * (float)velocity.Z;
                    } else {
                        Propulsion = ForwardMultiplier * Power * this.acceleration / 5 * this.Gear;
                    }
                    wheel.PropulsionOverride = Propulsion;
                    wheel.SteeringOverride = steerValue;
                }
            }
        }

        //Rover Class END ---------------------------------------------------------------------------------------------------

        //Main Program ------------------------------------------------------------------------------------------------------



        public static RoverController Rover;



        // Constructor - 
        public Program ()
        {
            Rover = new RoverController();
            this.GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(Rover.wheels);
            Rover.MainControl = GetMainShipController();
        }

        // To Save Custom Data
        public void Save () { }


        // Main Update Loop
        public void Main (string argument, UpdateType updateSource)
        {

            if (Rover.MainControl == null) {
                Echo("Unable to find main controller");
                return;
            }

            ProcessCommands(argument);

            Rover.UpdateStats(argument);
            Rover.ControlVehicle();
        }


        // Helper Class to Get the Main Control Station
        IMyShipController GetMainShipController ()
        {
            List<IMyShipController> shipControllers = new List<IMyShipController>();
            this.GridTerminalSystem.GetBlocksOfType<IMyShipController>(shipControllers);

            foreach (IMyShipController thisController in shipControllers) {
                if (thisController.IsMainCockpit)
                    return thisController;
                else if (thisController.ControlWheels && thisController.IsUnderControl && thisController.CanControlShip)
                    return thisController;
            }
            return null;
        }

        // Helper Class to Process Command Line Arguments
        void ProcessCommands (string argument)
        {
            if (!string.IsNullOrWhiteSpace(argument)) {
                switch (argument) {
                    case "gear_down":
                        if (Rover.Gear >= 0) { Rover.Gear--; }
                        break;
                    case "gear_up":
                        if (Rover.Gear <= 4) { Rover.Gear++; }
                        break;
                }
            }
        }

        //Main Program END --------------------------------------------------------------------------------------------------

    }
}