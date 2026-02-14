import React, { useState } from 'react';
import { X, Plus, Building2, Check } from 'lucide-react';
import { useAppStore } from '@/store/app-store';
import { Tenant } from '@/types';
import { generateId } from '@/lib/utils';

interface Props {
  onClose: () => void;
}

export const TenantSwitcher: React.FC<Props> = ({ onClose }) => {
  const { tenants, currentTenant, setCurrentTenant, addTenant, removeTenant } = useAppStore();
  const [showAddForm, setShowAddForm] = useState(false);
  const [newTenantName, setNewTenantName] = useState('');
  const [newTenantApiKey, setNewTenantApiKey] = useState('secure_password');

  const handleAddTenant = () => {
    if (!newTenantName.trim()) return;

    const newTenant: Tenant = {
      id: `tenant-${generateId()}`,
      name: newTenantName.trim(),
      apiKey: newTenantApiKey.trim() || 'secure_password',
      color: `#${Math.floor(Math.random() * 16777215).toString(16)}`,
    };

    addTenant(newTenant);
    setNewTenantName('');
    setNewTenantApiKey('secure_password');
    setShowAddForm(false);
  };

  const handleSelectTenant = (tenant: Tenant) => {
    setCurrentTenant(tenant);
    onClose();
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl shadow-xl max-w-md w-full max-h-[80vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <Building2 className="w-6 h-6 text-primary-600" />
            <h2 className="text-xl font-semibold text-gray-900">Workspaces</h2>
          </div>
          <button
            onClick={onClose}
            className="p-2 text-gray-400 hover:text-gray-600 rounded-lg hover:bg-gray-100"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6 space-y-3">
          {/* Current tenants */}
          {tenants.map((tenant) => (
            <div
              key={tenant.id}
              onClick={() => handleSelectTenant(tenant)}
              className={`
                flex items-center gap-3 p-4 rounded-lg border-2 cursor-pointer transition-all
                ${
                  currentTenant?.id === tenant.id
                    ? 'border-primary-500 bg-primary-50'
                    : 'border-gray-200 hover:border-gray-300 hover:bg-gray-50'
                }
              `}
            >
              <div
                className="w-10 h-10 rounded-lg flex items-center justify-center text-white font-semibold"
                style={{ backgroundColor: tenant.color || '#0ea5e9' }}
              >
                {tenant.name[0].toUpperCase()}
              </div>
              <div className="flex-1 min-w-0">
                <div className="font-medium text-gray-900">{tenant.name}</div>
                <div className="text-sm text-gray-500 truncate">ID: {tenant.id}</div>
              </div>
              {currentTenant?.id === tenant.id && (
                <Check className="w-5 h-5 text-primary-600 flex-shrink-0" />
              )}
              {tenant.id !== 'default' && (
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    if (confirm(`Delete workspace "${tenant.name}"?`)) {
                      removeTenant(tenant.id);
                    }
                  }}
                  className="p-2 text-gray-400 hover:text-red-600 rounded-lg hover:bg-red-50"
                >
                  <X className="w-4 h-4" />
                </button>
              )}
            </div>
          ))}

          {/* Add new tenant */}
          {showAddForm ? (
            <div className="p-4 bg-gray-50 rounded-lg border-2 border-dashed border-gray-300 space-y-3">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Workspace Name
                </label>
                <input
                  type="text"
                  value={newTenantName}
                  onChange={(e) => setNewTenantName(e.target.value)}
                  placeholder="Acme Corp"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                  autoFocus
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  API Key (optional)
                </label>
                <input
                  type="text"
                  value={newTenantApiKey}
                  onChange={(e) => setNewTenantApiKey(e.target.value)}
                  placeholder="secure_password"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
              </div>
              <div className="flex gap-2">
                <button
                  onClick={handleAddTenant}
                  disabled={!newTenantName.trim()}
                  className="flex-1 px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:opacity-50 disabled:cursor-not-allowed font-medium"
                >
                  Add Workspace
                </button>
                <button
                  onClick={() => {
                    setShowAddForm(false);
                    setNewTenantName('');
                  }}
                  className="px-4 py-2 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 text-gray-700 font-medium"
                >
                  Cancel
                </button>
              </div>
            </div>
          ) : (
            <button
              onClick={() => setShowAddForm(true)}
              className="w-full flex items-center justify-center gap-2 p-4 border-2 border-dashed border-gray-300 rounded-lg hover:border-gray-400 hover:bg-gray-50 text-gray-600 transition-colors"
            >
              <Plus className="w-5 h-5" />
              <span className="font-medium">Add New Workspace</span>
            </button>
          )}
        </div>

        {/* Footer info */}
        <div className="px-6 py-4 border-t border-gray-200 bg-gray-50">
          <p className="text-xs text-gray-600">
            Workspaces allow you to manage separate data sets with their own API keys and tenant IDs.
            Documents are isolated per workspace.
          </p>
        </div>
      </div>
    </div>
  );
};
