﻿using System;
using System.Threading;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using Rage;
using Rage.Forms;
using Gwen.Control;
using LSPD_First_Response.Engine.Scripting.Entities;
using LSPD_First_Response.Mod.API;

namespace ComputerPlus
{
    internal class ComputerVehDB : GwenForm
    {
        private Button btn_search, btn_main;
        private MultilineTextBox output_info;
        private TextBox input_name;
        internal static GameFiber form_main = new GameFiber(OpenMainMenuForm);
        internal static GameFiber search_fiber = new GameFiber(null);
        private bool _initial_clear = false;

        public ComputerVehDB() : base(typeof(ComputerVehDBTemplate))
        {

        }

        public override void InitializeLayout()
        {
            base.InitializeLayout();
            this.btn_search.Clicked += this.SearchButtonClickedHandler;
            this.btn_main.Clicked += this.MainMenuButtonClickedHandler;
            this.input_name.Clicked += this.InputNameFieldClickedHandler;
            this.input_name.SubmitPressed += this.InputNameSubmitHandler;
            this.Position = new Point(Game.Resolution.Width / 2 - this.Window.Width / 2, Game.Resolution.Height / 2 - this.Window.Height / 2);
            this.Window.DisableResizing();
            output_info.KeyboardInputEnabled = false;
            if (Functions.GetCurrentPullover() != null)
            {
                Ped ped = Functions.GetPulloverSuspect(Functions.GetCurrentPullover());
                if (ped.LastVehicle != null)
                {
                    input_name.Text = ped.LastVehicle.LicensePlate;
                    _initial_clear = true;
                }
            }
        }

        private void InputNameSubmitHandler(Base sender, EventArgs e)
        {
            SearchForVehicle();
        }

        private void SearchButtonClickedHandler(Base sender, ClickedEventArgs e)
        {
            SearchForVehicle();
        }

        private void MainMenuButtonClickedHandler(Base sender, ClickedEventArgs e)
        {
            this.Window.Close();
            form_main = new GameFiber(OpenMainMenuForm);
            form_main.Start();
        }

        private void InputNameFieldClickedHandler(Base sender, ClickedEventArgs e)
        {
            if (!_initial_clear)
            {
                input_name.Text = "";
                _initial_clear = true;
            }
        }

        private static void OpenMainMenuForm()
        {
            GwenForm main = new ComputerMain();
            main.Show();
            while (main.Window.IsVisible)
                GameFiber.Yield();
        }

        private void SearchForVehicle()
        {
            if (!search_fiber.IsAlive)
            {
                output_info.Text = "Searching. Please wait...";
                search_fiber = GameFiber.StartNew(delegate
                {
                    GameFiber.Sleep(2500);
                    string lp_input = input_name.Text.ToLower();
                    List<Vehicle> vehs = World.GetAllVehicles().ToList();
                    vehs.RemoveAll(v => !v);
                    vehs.OrderBy(v => v.DistanceTo(Game.LocalPlayer.Character.Position));
                    Vehicle veh = vehs.Where(v => v && v.LicensePlate.ToLower().Trim() == lp_input).FirstOrDefault();

                    if (veh)
                    {
                        output_info.Text = GetFormattedInfoForVehicle(veh);
                        Function.AddVehicleToRecents(veh);
                    }
                    else
                    {
                        output_info.Text = "No record for the specified license plate was found.";
                    }
                });
            }
        }

        private string GetFormattedInfoForVehicle(Vehicle veh)
        {
            string info = "";
            string veh_name = Function.GetVehicleDisplayName(veh);
            string veh_owner = Functions.GetVehicleOwnerName(veh);
            info = String.Format("Information found for license plate \"{0}\":\nVehicle: {1}\nOwner: {2}", veh.LicensePlate.Trim(),
                veh_name, veh_owner);

            if (Function.IsTrafficPolicerRunning())
            {
                string reg_text = TrafficPolicerFunction.GetVehicleRegistrationStatus(veh).ToString();
                info += String.Format("\nRegistration Status: {0}", reg_text);

                string insurance_text = TrafficPolicerFunction.GetVehicleInsuranceStatus(veh).ToString();
                info += String.Format("\nInsurance Status: {0}", insurance_text);
            }

            if (veh.IsStolen)
            {
                info += "\nThis vehicle has been reported as stolen.";
            }

            List<Ped> peds = World.GetAllPeds().ToList();
            peds.RemoveAll(p => !p);
            peds.OrderBy(p => Vector3.Distance(Game.LocalPlayer.Character.CurrentVehicle.Position, p.Position));
            Ped ped = peds.Where(p => p && Functions.GetPersonaForPed(p).FullName == veh_owner).FirstOrDefault();
            if (ped)
            {
                Persona p = Functions.GetPersonaForPed(ped);
                string wanted_text = "No active warrant(s)", leo_text = "";
                if (p.Wanted)
                    wanted_text = "Suspect has an active warrant";
                if (p.IsCop)
                    leo_text = "Note: Suspect is an off-duty police officer";
                else if (p.IsAgent)
                    leo_text = "Note: Suspect is a federal agent";
                info += String.Format("\n\nInformation found about vehicle owner \"{0}\":\nDOB: {1}\nCitations: {2}\nGender: {3}\nLicense: {4}\n"
                    + "Times Stopped: {5}\nWanted: {6}\n{7}", p.FullName, String.Format("{0:dddd, MMMM dd, yyyy}", p.BirthDay), p.Citations, p.Gender, p.LicenseState,
                    p.TimesStopped, wanted_text, leo_text);
            }
            return info;
        }
    }
}
