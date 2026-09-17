import React from 'react';
import { Settings } from 'lucide-react';

export function SettingsView({
  section,
  language,
  t,
  onSectionChange,
  onLanguageChange,
}: {
  section: string;
  language: 'tr' | 'en';
  t: (key: string, values?: Record<string, unknown>) => string;
  onSectionChange: (section: string) => void;
  onLanguageChange: (language: 'tr' | 'en') => void;
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
        {(section === 'notifications' || section === 'about') && (
          <p className="notice">{t('settingsNotice')}</p>
        )}
      </div>
    </section>
  );
}
