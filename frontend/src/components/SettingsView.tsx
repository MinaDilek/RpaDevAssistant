import React from 'react';
import { Settings } from 'lucide-react';
import type { DesktopUpdateStatus } from '../services/updateService';

export function SettingsView({
  section,
  language,
  t,
  onSectionChange,
  onLanguageChange,
  updateStatus,
  onCheckForUpdates,
  onInstallUpdate,
}: {
  section: string;
  language: 'tr' | 'en';
  t: (key: string, values?: Record<string, unknown>) => string;
  onSectionChange: (section: string) => void;
  onLanguageChange: (language: 'tr' | 'en') => void;
  updateStatus: DesktopUpdateStatus;
  onCheckForUpdates: () => void;
  onInstallUpdate: () => void;
}) {
  const sections = [
    'profileSettings',
    'appearance',
    'languageSettings',
    'reviewRules',
    'notifications',
    'about',
  ];

  return (
    <section className="settings-layout" aria-label={t('settings')}>
      <aside className="settings-nav">
        {sections.map((item) => (
          <button
            key={item}
            type="button"
            className={section === item ? 'active' : ''}
            onClick={() => onSectionChange(item)}
          >
            {t(item)}
          </button>
        ))}
      </aside>
      <div className="settings-panel">
        <div className="section-header">
          <div>
            <span className="eyebrow">{t('settings')}</span>
            <h2>{t(section)}</h2>
          </div>
          <Settings size={20} />
        </div>
        {section === 'profileSettings' && (
          <div className="profile-settings">
            <span className="avatar large">MD</span>
            <div>
              <h3>Mina Dilek</h3>
              <p>RPA Developer</p>
              <p>mina@example.local</p>
            </div>
          </div>
        )}
        {section === 'appearance' && (
          <div className="option-grid">
            {['lightTheme', 'darkTheme', 'systemTheme'].map((mode) => (
              <button key={mode} type="button">
                {t(mode)}
              </button>
            ))}
          </div>
        )}
        {section === 'languageSettings' && (
          <div className="option-grid">
            <button
              className={language === 'tr' ? 'active' : ''}
              type="button"
              onClick={() => onLanguageChange('tr')}
            >
              Türkçe
            </button>
            <button
              className={language === 'en' ? 'active' : ''}
              type="button"
              onClick={() => onLanguageChange('en')}
            >
              English
            </button>
          </div>
        )}
        {section === 'reviewRules' && (
          <div className="settings-checks two-column">
            {[
              'selectorQuality',
              'exceptionHandling',
              'logging',
              'namingConvention',
              'hardcodedValues',
              'performance',
              'maintainability',
              'security',
              'reframeworkBestPractices',
            ].map((label) => (
              <label key={label}>
                <input type="checkbox" defaultChecked /> {t(label)}
              </label>
            ))}
          </div>
        )}
        {section === 'notifications' && (
          <p className="notice">{t('settingsNotice')}</p>
        )}
        {section === 'about' && (
          <div className="update-panel">
            <h3>{t('desktopUpdates')}</h3>
            {!updateStatus.supported && <p className="notice">{t('updatesDesktopOnly')}</p>}
            {updateStatus.supported && updateStatus.checking && <p>{t('checkingForUpdates')}</p>}
            {updateStatus.supported && !updateStatus.checking && !updateStatus.available && !updateStatus.error && <p>{t('appIsUpToDate')}</p>}
            {updateStatus.error && <p className="error-text">{t('updateCheckFailed')}</p>}
            {updateStatus.available && (
              <div className="update-available">
                <strong>{t('updateAvailable', { version: updateStatus.version ?? '-' })}</strong>
                {updateStatus.notes && <p>{updateStatus.notes}</p>}
                <button type="button" disabled={updateStatus.installing} onClick={onInstallUpdate}>
                  {updateStatus.installing ? t('installingUpdate') : t('downloadAndInstallUpdate')}
                </button>
              </div>
            )}
            {updateStatus.supported && !updateStatus.installing && (
              <button type="button" onClick={onCheckForUpdates}>{t('checkForUpdates')}</button>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
