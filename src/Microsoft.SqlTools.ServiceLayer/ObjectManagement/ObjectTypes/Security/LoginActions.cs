//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.ServiceLayer.Management;

namespace Microsoft.SqlTools.ServiceLayer.ObjectManagement
{
    internal class LoginActions : ManagementActionBase
    {
        private ConfigAction configAction;

        private LoginPrototype prototype;

        /// <summary>
        /// Handle login create and update actions
        /// </summary>        
        public LoginActions(CDataContainer dataContainer, ConfigAction configAction, LoginPrototype prototype)
        {
            this.DataContainer = dataContainer;
            this.configAction = configAction;
            this.prototype = prototype;
        }

        /// <summary>
        /// called by the management actions framework to execute the action
        /// </summary>
        /// <param name="node"></param>
        public override void OnRunNow(object sender)
        {
            if (this.configAction != ConfigAction.Drop)
            {
                prototype.ApplyGeneralChanges(this.DataContainer.Server);
                prototype.ApplyServerRoleChanges(this.DataContainer.Server);
                prototype.ApplyDatabaseRoleChanges(this.DataContainer.Server);
            }
        }
    }
}