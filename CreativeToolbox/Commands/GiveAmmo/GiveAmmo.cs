﻿using System;
using CommandSystem;
using Exiled.Permissions.Extensions;

namespace CreativeToolbox.Commands.GiveAmmo
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class GiveAmmo : ParentCommand
    {
        public GiveAmmo() => LoadGeneratedCommands();

        public override string Command { get; } = "giveammo";

        public override string[] Aliases { get; } = new string[] { };

        public override string Description { get; } = "Gives a specified user or users a specified ammount of a given ammo type";

        public override void LoadGeneratedCommands()
        {
            RegisterCommand(new All());
            RegisterCommand(new To());
        }

        protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!(sender as CommandSender).CheckPermission("ct.giveammo"))
            {
                response = "You do not have permission to run this command! Missing permission: \"ct.giveammo\"";
                return false;
            }

            response = "Please enter a valid subcommand! Available ones: all, to";
            return false;
        }
    }
}
