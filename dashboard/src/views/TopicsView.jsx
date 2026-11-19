import { Route, Routes } from 'react-router-dom';
import TopicsList from './topics/TopicsList.jsx';
import TopicWorkspace from './topics/TopicWorkspace.jsx';
import RefinementWizard from './topics/wizard/RefinementWizard.jsx';

// /topics, /topics/new, /topics/:id/refine, and /topics/:id.
export default function TopicsView() {
  return (
    <Routes>
      <Route index element={<TopicsList />} />
      <Route path="new" element={<RefinementWizard mode="new" />} />
      <Route path=":id/refine" element={<RefinementWizard mode="refine" />} />
      <Route path=":id" element={<TopicWorkspace />} />
    </Routes>
  );
}
