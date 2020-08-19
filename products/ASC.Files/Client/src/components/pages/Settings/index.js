import React from 'react';
import { connect } from 'react-redux';
import { withRouter } from "react-router";
import { PageLayout, utils } from "asc-web-common";
import {
  ArticleHeaderContent,
  ArticleBodyContent,
  ArticleMainButtonContent
} from "../../Article";
import { SectionHeaderContent, SectionBodyContent } from "./Section";

import { withTranslation, I18nextProvider } from "react-i18next";
import { createI18N } from "../../../helpers/i18n";

import { setSettingsIsLoad } from '../../../store/files/actions'

const i18n = createI18N({
  page: "SettingsTree",
  localesPath: "pages/Settings"
})

const { changeLanguage } = utils;

class PureSettings extends React.Component {
  constructor(props) {
    super(props)

    this.state = {
      intermediateVersion: false,
      thirdParty: false
    }
  }

  isCheckedIntermediate = () => {
    this.setState({
      intermediateVersion: !this.state.intermediateVersion
    })
  }

  isCheckedThirdParty = () => {
    this.setState({
      thirdParty: !this.state.thirdParty
    })
  }

  renderCommonSettings = () => {
    return <span>CommonSettings</span>
  }

  renderClouds = () => {
    return <span>Clouds</span>
  }

  render() {
    console.log('render settings');
    const { 
      intermediateVersion,
      thirdParty
    } = this.state;
    const { match, t } = this.props;
    const { setting } = match.params;

    return (
      <PageLayout>
        <PageLayout.ArticleHeader>
          <ArticleHeaderContent />
        </PageLayout.ArticleHeader>

        <PageLayout.ArticleMainButton>
          <ArticleMainButtonContent />
        </PageLayout.ArticleMainButton>

        <PageLayout.ArticleBody>
          <ArticleBodyContent />
        </PageLayout.ArticleBody>

        <PageLayout.SectionHeader>
          <SectionHeaderContent title={t(`${setting}`)}/>
        </PageLayout.SectionHeader>

        <PageLayout.SectionBody>
          <SectionBodyContent
            setting={setting}
            thirdParty={thirdParty}
            intermediateVersion={intermediateVersion}
            isCheckedThirdParty={this.isCheckedThirdParty}
            isCheckedIntermediate={this.isCheckedIntermediate}
          />
        </PageLayout.SectionBody>
      </PageLayout>
    );
  }
} 

const SettingsContainer = withTranslation()(PureSettings);

const Settings = props => {
  changeLanguage(i18n);
  return (
    <I18nextProvider i18n={i18n}>
      <SettingsContainer {...props} />
    </I18nextProvider>
  );
}

function mapStateToProps(state) {
  const { settingsIsLoad } = state.files;

  return { 
    settingsIsLoad
  };
}

export default connect(
  mapStateToProps,
  {
    setSettingsIsLoad
  }
)(withRouter(Settings));